using System.Runtime.InteropServices;
using Accessibility;
using Clemotius.Interop;

namespace Clemotius.Scroll;

/// <summary>カーソル直下のスクロールバーの向き。</summary>
internal enum ScrollBarHit
{
    None,
    Horizontal,
    Vertical,
}

/// <summary>
/// カーソル直下がスクロールバーかどうか、およびその向きを判定する。4段構え:
///   1. 単独スクロールバー（独立した "ScrollBar" コントロール）: クラス名＋スタイルで判定
///   2. 標準スクロールバー（ウィンドウ非クライアントの WS_HSCROLL/WS_VSCROLL）: WM_NCHITTEST で判定
///   3. カスタム描画スクロールバー: MSAA の ROLE_SYSTEM_SCROLLBAR
///   4. Chromium 系: MSAA は a11y 既定無効でスタブしか返さないため、UIA の
///      ScrollPattern 要素＋端帯ジオメトリ（<see cref="ScrollBarBand"/>）で判定。
///      スクロール自体は WM_VSCROLL/WM_HSCROLL が Chromium にも効くことを実測確認済み。
/// </summary>
internal static class ScrollBarDetector
{
    private const int GWL_STYLE = -16;
    private const int SBS_VERT = 0x0001; // 立っていれば垂直（単独スクロールバー）

    // 検出はクロスプロセス呼び出し（NCHITTEST/MSAA/UIA）を含み、相手がビジーだと
    // 長くブロックしうるため、連続ホイール中は近傍・短時間の結果を再利用する。
    // 参照の差し替えがアトミックになるよう不変レコードで保持する
    // （フックスレッドとバックグラウンド検出スレッドの両方から書くため）。
    private sealed record CacheEntry(int X, int Y, ScrollBarHit Hit, nint Target, uint Tick);

    private static volatile CacheEntry? _cache;
    private static int _probing; // MSAA/UIA バックグラウンド検出の多重起動防止 (0/1)

    /// <summary>
    /// カーソル直下のスクロールバーの向きと、スクロールメッセージの送出先ウィンドウを返す。
    /// 検出できなければ (None, 0)。
    ///
    /// フックスレッドの停止時間を最大 ~30ms に抑えるため、同期的に行うのは
    /// クラス名判定と NCHITTEST（30ms 打ち切り）まで。MSAA/UIA はバックグラウンドで
    /// 計算してキャッシュへ反映する（その間の数十 ms は素通し扱い）。
    /// タイムアウトなしの同期呼び出しは、高負荷アプリ（画像ビューアの読み込み中等）で
    /// フックごと固まり、入力キュー溢れ＝連続ビープ音の原因になる。
    /// </summary>
    public static (ScrollBarHit hit, nint target) Detect(int x, int y)
    {
        var c = _cache;
        uint now = (uint)Environment.TickCount;
        if (c is not null && now - c.Tick < 250 && Math.Abs(x - c.X) < 8 && Math.Abs(y - c.Y) < 8)
            return (c.Hit, c.Target);

        var pt = new NativeMethods.POINT { X = x, Y = y };
        nint hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd == 0)
            return Store(x, y, ScrollBarHit.None, 0);

        // 1) 単独スクロールバー コントロール → 親ウィンドウへ送る
        if (GetClassName(hwnd).Equals("ScrollBar", StringComparison.OrdinalIgnoreCase))
        {
            int style = InputNative.GetWindowLongW(hwnd, GWL_STYLE);
            var dir = (style & SBS_VERT) != 0 ? ScrollBarHit.Vertical : ScrollBarHit.Horizontal;
            return Store(x, y, dir, hwnd);
        }

        // 2) 非クライアントの標準スクロールバー: 30ms 打ち切りでヒットテスト
        nint lParam = unchecked((nint)((y << 16) | (x & 0xFFFF)));
        if (InputNative.SendMessageTimeoutW(
                hwnd, InputNative.WM_NCHITTEST, 0, lParam,
                InputNative.SMTO_ABORTIFHUNG, 30, out nint hit) == 0)
        {
            // 応答しない相手には MSAA/UIA も掛けない
            return Store(x, y, ScrollBarHit.None, 0);
        }
        switch ((int)hit)
        {
            case InputNative.HTHSCROLL:
                return Store(x, y, ScrollBarHit.Horizontal, hwnd);
            case InputNative.HTVSCROLL:
                return Store(x, y, ScrollBarHit.Vertical, hwnd);
        }

        // 3)+4) カスタム描画スクロールバー（MSAA → UIA）: 相手プロセス次第で
        // 数秒ブロックしうるため、フックスレッドでは待たずに別スレッドで計算して
        // キャッシュへ反映する。完了までは暫定で「スクロールバーでない」をキャッシュし、
        // 連続ホイール中の NCHITTEST 再実行（30ms×N）も防ぐ。
        if (Interlocked.CompareExchange(ref _probing, 1, 0) == 0)
        {
            Task.Run(() =>
            {
                try
                {
                    var r = DetectByMsaaCore(x, y);
                    if (r.hit == ScrollBarHit.None)
                        r = DetectByUia(x, y, hwnd);
                    _cache = new CacheEntry(x, y, r.hit, r.target, (uint)Environment.TickCount);
                }
                finally
                {
                    Volatile.Write(ref _probing, 0);
                }
            });
        }
        return Store(x, y, ScrollBarHit.None, 0); // 暫定値（プローブ完了時に上書きされる）
    }

    private static (ScrollBarHit hit, nint target) Store(int x, int y, ScrollBarHit hit, nint target)
    {
        _cache = new CacheEntry(x, y, hit, target, (uint)Environment.TickCount);
        return (hit, target);
    }

    private const int ROLE_SYSTEM_SCROLLBAR = 3;

    // ── 4) UIA: ScrollPattern を持つ要素＋端帯ジオメトリ ──
    // Chromium はスクロールバーを UIA の ScrollBar 要素として公開しないため、
    // 「スクロール可能要素の右端/下端のスクロールバー幅の帯」をスクロールバー扱いする。
    private static (ScrollBarHit hit, nint target) DetectByUia(int x, int y, nint hwnd)
    {
        try
        {
            var el = System.Windows.Automation.AutomationElement.FromPoint(
                new System.Windows.Point(x, y));
            for (int depth = 0; el is not null && depth < 8; depth++)
            {
                if (el.TryGetCurrentPattern(
                        System.Windows.Automation.ScrollPattern.Pattern, out object p))
                {
                    var pattern = (System.Windows.Automation.ScrollPattern)p;
                    var rect = el.Current.BoundingRectangle;
                    var hit = Clemotius.Core.Scroll.ScrollBarBand.Hit(
                        x, y,
                        (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height,
                        pattern.Current.VerticallyScrollable,
                        pattern.Current.HorizontallyScrollable,
                        InputNative.GetSystemMetrics(InputNative.SM_CXVSCROLL),
                        InputNative.GetSystemMetrics(InputNative.SM_CYHSCROLL));
                    return hit switch
                    {
                        Clemotius.Core.Scroll.BandHit.Vertical => (ScrollBarHit.Vertical, hwnd),
                        Clemotius.Core.Scroll.BandHit.Horizontal => (ScrollBarHit.Horizontal, hwnd),
                        // 最初に見つかったスクロール要素で判定を終える（外側へは波及させない）
                        _ => (ScrollBarHit.None, 0),
                    };
                }
                el = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(el);
            }
        }
        catch (System.Windows.Automation.ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        return (ScrollBarHit.None, 0);
    }

    private static (ScrollBarHit hit, nint target) DetectByMsaaCore(int x, int y)
    {
        try
        {
            if (AccessibleObjectFromPoint(new POINTSTRUCT { x = x, y = y },
                    out IAccessible? acc, out object child) != 0 || acc is null)
            {
                return (ScrollBarHit.None, 0);
            }

            object childId = child ?? 0;
            // ヒットした要素がスクロールバーの子（ボタン/つまみ）のこともあるため親を少し遡る
            for (int depth = 0; depth < 3 && acc is not null; depth++)
            {
                if (RoleOf(acc, childId) == ROLE_SYSTEM_SCROLLBAR)
                {
                    acc.accLocation(out _, out _, out int w, out int h, childId);
                    if (w <= 0 || h <= 0)
                        return (ScrollBarHit.None, 0);
                    if (WindowFromAccessibleObject(acc, out nint hwnd) != 0 || hwnd == 0)
                        return (ScrollBarHit.None, 0);
                    return (w >= h ? ScrollBarHit.Horizontal : ScrollBarHit.Vertical, hwnd);
                }
                acc = acc.accParent as IAccessible;
                childId = 0; // 親へ遡ったら自身を指す
            }
        }
        catch (COMException) { }
        catch (InvalidCastException) { }
        catch (ArgumentException) { }
        return (ScrollBarHit.None, 0);
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTSTRUCT { public int x, y; }

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromPoint(
        POINTSTRUCT pt, out IAccessible? ppacc, out object pvarChild);

    [DllImport("oleacc.dll")]
    private static extern int WindowFromAccessibleObject(IAccessible pacc, out nint phwnd);

    private static string GetClassName(nint hwnd)
    {
        var buffer = new char[64];
        int len = InputNative.GetClassNameW(hwnd, buffer, buffer.Length);
        return len > 0 ? new string(buffer, 0, len) : "";
    }
}
