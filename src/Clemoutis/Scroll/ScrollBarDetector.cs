using System.Runtime.InteropServices;
using Accessibility;
using Clemoutis.Interop;

namespace Clemoutis.Scroll;

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

    /// <summary>
    /// カーソル直下のスクロールバーの向きと、スクロールメッセージの送出先ウィンドウを返す。
    /// 検出できなければ (None, 0)。
    /// </summary>
    public static (ScrollBarHit hit, nint target) Detect(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        nint hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd == 0)
            return (ScrollBarHit.None, 0);

        // 1) 単独スクロールバー コントロール → 親ウィンドウへ送る
        if (GetClassName(hwnd).Equals("ScrollBar", StringComparison.OrdinalIgnoreCase))
        {
            int style = InputNative.GetWindowLongW(hwnd, GWL_STYLE);
            var dir = (style & SBS_VERT) != 0 ? ScrollBarHit.Vertical : ScrollBarHit.Horizontal;
            return (dir, hwnd);
        }

        // 2) 非クライアントの標準スクロールバー: ウィンドウにヒットテストを問い合わせる
        nint lParam = unchecked((nint)((y << 16) | (x & 0xFFFF)));
        nint hit = InputNative.SendMessageW(hwnd, InputNative.WM_NCHITTEST, 0, lParam);
        switch ((int)hit)
        {
            case InputNative.HTHSCROLL:
                return (ScrollBarHit.Horizontal, hwnd);
            case InputNative.HTVSCROLL:
                return (ScrollBarHit.Vertical, hwnd);
        }

        // 3)+4) カスタム描画スクロールバー: MSAA → UIA の順で問い合わせる
        return DetectCustom(x, y, hwnd);
    }

    private const int ROLE_SYSTEM_SCROLLBAR = 3;

    // MSAA/UIA はプロセス間呼び出しで数 ms かかりうるため、連続ホイール中は
    // 近傍・短時間の結果を再利用してラグを防ぐ
    private static (int x, int y, ScrollBarHit hit, nint target, uint tick) _msaaCache;

    private static (ScrollBarHit hit, nint target) DetectCustom(int x, int y, nint hwnd)
    {
        var c = _msaaCache;
        uint now = (uint)Environment.TickCount;
        if (now - c.tick < 250 && Math.Abs(x - c.x) < 8 && Math.Abs(y - c.y) < 8)
            return (c.hit, c.target);

        var result = DetectByMsaaCore(x, y);
        if (result.hit == ScrollBarHit.None)
            result = DetectByUia(x, y, hwnd);
        _msaaCache = (x, y, result.hit, result.target, now);
        return result;
    }

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
                    var hit = Clemoutis.Core.Scroll.ScrollBarBand.Hit(
                        x, y,
                        (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height,
                        pattern.Current.VerticallyScrollable,
                        pattern.Current.HorizontallyScrollable,
                        InputNative.GetSystemMetrics(InputNative.SM_CXVSCROLL),
                        InputNative.GetSystemMetrics(InputNative.SM_CYHSCROLL));
                    return hit switch
                    {
                        Clemoutis.Core.Scroll.BandHit.Vertical => (ScrollBarHit.Vertical, hwnd),
                        Clemoutis.Core.Scroll.BandHit.Horizontal => (ScrollBarHit.Horizontal, hwnd),
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
