using System.Runtime.InteropServices;
using Accessibility;
using Clemotius.Interop;

namespace Clemotius.Gestures;

/// <summary>
/// 右DOWN位置がファイル/フォルダ等の「ドラッグ可能な項目」の上かを MSAA で判定する。
/// 項目上なら右ボタンをアプリへ透過し、アプリ独自の右ドラッグ（例: エクスプローラの
/// ファイル/フォルダの右ドラッグ）を成立させる。項目の無い背景上ならジェスチャーを扱う。
///
/// 設計原則（フックスレッドで無制限の同期クロスプロセス呼び出しをしない）に従い、MSAA 呼び出しは
/// 必ずバックグラウンドで行い、フックスレッドは一切ブロックしない（ScrollBarDetector と同方式）。
/// <see cref="Prime"/> をマウス移動中に呼んでキャッシュを温めておくことで、右DOWNの瞬間には
/// すでに判定済みになっており、ブロックも取りこぼしもなく項目/背景を即答できる。万一キャッシュが
/// 無い場合は <see cref="IsOverDraggableItem"/> は false（ジェスチャー優先）を返す（取りこぼし防止）。
/// </summary>
internal static class RightDragItemDetector
{
    // MSAA ロール（oleacc）
    private const int ROLE_SYSTEM_LISTITEM = 34;   // リスト項目（ファイル/フォルダ等）
    private const int ROLE_SYSTEM_OUTLINEITEM = 35; // ツリー項目（フォルダツリー等）

    private const int NearPx = 8;
    // 右DOWN読み出しの有効期間。静止カーソル下の項目/背景はしばらく変わらないため長め。
    private const uint ReadFreshMs = 2000;
    // 移動中の再判定しきい。これより古ければホバー先読みで温め直す。
    private const uint PrimeFreshMs = 400;
    // バックグラウンド判定の最小起動間隔（先読みの多重・過剰起動を抑える）。
    private const uint MinProbeIntervalMs = 60;

    private sealed record CacheEntry(int X, int Y, bool IsItem, uint Tick);
    private static volatile CacheEntry? _cache;
    private static int _probing;            // 単発起動ガード (0/1)
    private static uint _lastProbeStartTick; // 直近のバックグラウンド判定起動時刻

    /// <returns>項目（ファイル/フォルダ等）の上なら true。背景上・不明なら false（ジェスチャー優先）。</returns>
    public static bool IsOverDraggableItem(int x, int y)
    {
        if (Lookup(x, y, ReadFreshMs) is bool cached)
            return cached;
        // 温まっていなければフックスレッドは待たず、次回以降のため起動だけして今回はジェスチャー優先。
        KickProbe(x, y, force: true);
        return false;
    }

    /// <summary>マウス移動中の事前判定。右DOWN前にキャッシュを温める。</summary>
    public static void Prime(int x, int y)
    {
        if (Lookup(x, y, PrimeFreshMs) is not null)
            return; // 近傍に十分新しい結果あり
        KickProbe(x, y, force: false);
    }

    private static bool? Lookup(int x, int y, uint freshMs)
    {
        var c = _cache;
        if (c is not null && (uint)Environment.TickCount - c.Tick < freshMs
            && Math.Abs(x - c.X) < NearPx && Math.Abs(y - c.Y) < NearPx)
        {
            return c.IsItem;
        }
        return null;
    }

    private static void KickProbe(int x, int y, bool force)
    {
        uint now = (uint)Environment.TickCount;
        if (!force && now - _lastProbeStartTick < MinProbeIntervalMs)
            return;
        if (Interlocked.CompareExchange(ref _probing, 1, 0) != 0)
            return;
        _lastProbeStartTick = now;
        Task.Run(() =>
        {
            try
            {
                bool isItem = Probe(x, y);
                _cache = new CacheEntry(x, y, isItem, (uint)Environment.TickCount);
            }
            finally
            {
                Volatile.Write(ref _probing, 0);
            }
        });
    }

    private static bool Probe(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        nint hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd == 0)
            return false;

        // ブラウザ(Chromium/Gecko)は a11y 既定無効で MSAA が遅延・不定になりやすく、かつ
        // ファイル/フォルダ項目を持たない。クラス名で除外して MSAA 呼び出し自体を避ける
        // （Chromium のアクセシビリティを起こす副作用も防ぐ）。
        if (IsBrowserClass(GetClassName(hwnd)))
            return false;

        return ProbeMsaa(x, y);
    }

    private static bool ProbeMsaa(int x, int y)
    {
        try
        {
            if (AccessibleObjectFromPoint(new POINTSTRUCT { x = x, y = y },
                    out IAccessible? acc, out object child) != 0 || acc is null)
            {
                return false;
            }

            // ヒット要素が項目の子（アイコン/テキスト等）のこともあるため親を少し遡って項目を探す
            object childId = child ?? 0;
            for (int depth = 0; depth < 3 && acc is not null; depth++)
            {
                int role = RoleOf(acc, childId);
                if (role is ROLE_SYSTEM_LISTITEM or ROLE_SYSTEM_OUTLINEITEM)
                    return true;
                acc = acc.accParent as IAccessible;
                childId = 0; // 親へ遡ったら自身を指す
            }
        }
        catch (COMException) { }
        catch (InvalidCastException) { }
        catch (ArgumentException) { }
        return false;
    }

    private static bool IsBrowserClass(string cls)
        => cls.StartsWith("Chrome_", StringComparison.Ordinal)
        || cls.StartsWith("Mozilla", StringComparison.Ordinal);

    private static int RoleOf(IAccessible acc, object childId)
    {
        try
        {
            return acc.get_accRole(childId) is int role ? role : 0;
        }
        catch (COMException)
        {
            return 0;
        }
    }

    private static string GetClassName(nint hwnd)
    {
        var buffer = new char[64];
        int len = InputNative.GetClassNameW(hwnd, buffer, buffer.Length);
        return len > 0 ? new string(buffer, 0, len) : "";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTSTRUCT { public int x, y; }

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromPoint(
        POINTSTRUCT pt, out IAccessible? ppacc, out object pvarChild);
}
