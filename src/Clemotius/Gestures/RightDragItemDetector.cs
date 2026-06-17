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
/// すでに判定済みになっており、<see cref="TryKnownItem"/> がブロックも取りこぼしもなく項目/背景を
/// 即答できる。未確定（コールド）の場合は <see cref="ConfirmItemAsync"/> でバックグラウンド確定し、
/// 項目と分かれば呼び出し側がドラッグへ転換する（down-while-held）。
/// </summary>
internal static class RightDragItemDetector
{
    // MSAA ロール（oleacc）
    private const int ROLE_SYSTEM_LISTITEM = 34;   // リスト項目（ファイル/フォルダ等）
    private const int ROLE_SYSTEM_OUTLINEITEM = 35; // ツリー項目（フォルダツリー等）

    private const int NearPx = 8;
    // 右DOWN読み出しの有効期間。静止カーソル下の項目/背景はしばらく変わらないが、
    // ウィンドウ内容の変化に古い判定を使い続けないよう短めにする（hwnd 一致も必須にしている）。
    private const uint ReadFreshMs = 500;
    // 移動中の再判定しきい。これより古ければホバー先読みで温め直す。
    private const uint PrimeFreshMs = 400;
    // バックグラウンド判定の最小起動間隔（先読みの多重・過剰起動を抑える）。
    private const uint MinProbeIntervalMs = 60;
    // バックグラウンド判定の最大実行時間。MSAA が相手プロセス都合で長時間戻らないと、
    // 単純な 0/1 ガードでは解放されず以後の判定が永久に起動しなくなる。これを超えたリースは
    // 「詰まった」とみなして新しい判定が奪えるようにし、検出がグローバルに固まるのを防ぐ。
    private const uint ProbeMaxMs = 1000;

    // キャッシュは座標近傍だけでなく対象ウィンドウ(hwnd)一致も条件にする。同じ座標でも
    // 前面ウィンドウや内容が変われば古い項目判定を使わない（誤透過/誤開始を防ぐ）。
    private sealed record CacheEntry(int X, int Y, nint Hwnd, bool IsItem, uint Tick);
    private static volatile CacheEntry? _cache;
    private static uint _probeLease;         // バックグラウンド判定のリース (0=空き、それ以外=開始tick)
    private static uint _lastProbeStartTick; // 直近のバックグラウンド判定起動時刻

    /// <summary>マウス移動中の事前判定。右DOWN前にキャッシュを温める。</summary>
    public static void Prime(int x, int y)
    {
        nint hwnd = WindowAt(x, y);
        if (hwnd == 0)
            return;
        if (Lookup(x, y, hwnd, PrimeFreshMs) is not null)
            return; // 近傍・同一ウィンドウに十分新しい結果あり
        KickProbe(x, y, hwnd, force: false);
    }

    /// <summary>
    /// キャッシュにある確定結果のみを返す（プローブは起動しない）。窓が無ければ確定で false、
    /// 近傍に新しい結果が無ければ null（未確定＝コールド）。
    /// </summary>
    public static bool? TryKnownItem(int x, int y)
    {
        nint hwnd = WindowAt(x, y);
        if (hwnd == 0)
            return false;
        return Lookup(x, y, hwnd, ReadFreshMs);
    }

    /// <summary>
    /// 項目かどうかをバックグラウンドで確定し、結果をコールバックする（フックスレッドは待たない）。
    /// コールバックは必ず1回、フックスレッド以外（ThreadPool）で呼ばれる。コールド時に
    /// ジェスチャー保留へ入った後、項目と判明したらドラッグへ転換する（down-while-held）ために使う。
    /// </summary>
    public static void ConfirmItemAsync(int x, int y, Action<bool> onResult)
    {
        Task.Run(() =>
        {
            nint hwnd = WindowAt(x, y);
            bool isItem;
            if (hwnd == 0)
                isItem = false;
            else if (Lookup(x, y, hwnd, ReadFreshMs) is bool cached)
                isItem = cached;
            else
            {
                isItem = Probe(x, y, hwnd);
                _cache = new CacheEntry(x, y, hwnd, isItem, (uint)Environment.TickCount);
            }
            onResult(isItem);
        });
    }

    private static nint WindowAt(int x, int y)
        => InputNative.WindowFromPoint(new NativeMethods.POINT { X = x, Y = y });

    private static bool? Lookup(int x, int y, nint hwnd, uint freshMs)
    {
        var c = _cache;
        if (c is not null && c.Hwnd == hwnd
            && (uint)Environment.TickCount - c.Tick < freshMs
            && Math.Abs(x - c.X) < NearPx && Math.Abs(y - c.Y) < NearPx)
        {
            return c.IsItem;
        }
        return null;
    }

    private static void KickProbe(int x, int y, nint hwnd, bool force)
    {
        uint now = (uint)Environment.TickCount;
        if (!force && now - _lastProbeStartTick < MinProbeIntervalMs)
            return;
        uint lease = TryAcquireProbe();
        if (lease == 0)
            return;
        _lastProbeStartTick = now;
        Task.Run(() =>
        {
            try
            {
                bool isItem = Probe(x, y, hwnd);
                _cache = new CacheEntry(x, y, hwnd, isItem, (uint)Environment.TickCount);
            }
            finally
            {
                ReleaseProbe(lease);
            }
        });
    }

    // バックグラウンド判定のリースを取得する。空き、または期限超過で詰まったリースを奪えたら
    // その開始 tick（解放用トークン、0 は使わない）を返す。取れなければ 0。
    private static uint TryAcquireProbe()
    {
        uint now = (uint)Environment.TickCount;
        if (now == 0) now = 1; // 0 は「空き」を表すので避ける
        while (true)
        {
            uint cur = Volatile.Read(ref _probeLease);
            if (cur != 0 && now - cur < ProbeMaxMs)
                return 0; // 実行中かつ期限内
            if (Interlocked.CompareExchange(ref _probeLease, now, cur) == cur)
                return now; // 空き、または期限超過のリースを奪った
        }
    }

    // 自分のリースのままなら解放する。期限超過で横取りされていたら触らない。
    private static void ReleaseProbe(uint myToken)
        => Interlocked.CompareExchange(ref _probeLease, 0, myToken);

    private static bool Probe(int x, int y, nint hwnd)
    {
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
