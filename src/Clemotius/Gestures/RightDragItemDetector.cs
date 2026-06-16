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
/// 右DOWN時は近傍・短時間のキャッシュがあればそれを使い、無ければバックグラウンド判定を起動して
/// この回は <c>false</c>（ジェスチャー優先）を返す。「不明時は透過」にするとジェスチャーを取りこぼす
/// ため、不明時は必ずジェスチャー側に倒す。
/// </summary>
internal static class RightDragItemDetector
{
    // MSAA ロール（oleacc）
    private const int ROLE_SYSTEM_LISTITEM = 34;   // リスト項目（ファイル/フォルダ等）
    private const int ROLE_SYSTEM_OUTLINEITEM = 35; // ツリー項目（フォルダツリー等）

    private const uint CacheFreshMs = 400;
    private const int CacheNearPx = 8;

    private sealed record CacheEntry(int X, int Y, bool IsItem, uint Tick);
    private static volatile CacheEntry? _cache;
    private static int _probing; // バックグラウンド判定の多重起動防止 (0/1)

    /// <returns>項目（ファイル/フォルダ等）の上なら true。背景上・不明なら false（ジェスチャー優先）。</returns>
    public static bool IsOverDraggableItem(int x, int y)
    {
        var c = _cache;
        if (c is not null && (uint)Environment.TickCount - c.Tick < CacheFreshMs
            && Math.Abs(x - c.X) < CacheNearPx && Math.Abs(y - c.Y) < CacheNearPx)
        {
            return c.IsItem;
        }

        // フックスレッドは待たない。次回以降のためにバックグラウンドで判定してキャッシュへ反映し、
        // 今回はジェスチャーを優先する（取りこぼし防止）。
        KickProbe(x, y);
        return false;
    }

    private static void KickProbe(int x, int y)
    {
        if (Interlocked.CompareExchange(ref _probing, 1, 0) != 0)
            return;
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
