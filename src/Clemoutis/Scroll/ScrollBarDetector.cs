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
/// カーソル直下がスクロールバーかどうか、およびその向きを判定する。
/// オリジナルが区別する2種をカバーする:
///   - 単独スクロールバー（独立した "ScrollBar" コントロール）: クラス名＋スタイルで判定
///   - 標準スクロールバー（ウィンドウ非クライアントの WS_HSCROLL/WS_VSCROLL）: WM_NCHITTEST で判定
/// ブラウザ等のカスタム描画スクロールバー（実 Win32 スクロールバーでない）は検出できない。
/// より厳密な判定は MSAA で（設計の RE 課題）。
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
        return (int)hit switch
        {
            InputNative.HTHSCROLL => (ScrollBarHit.Horizontal, hwnd),
            InputNative.HTVSCROLL => (ScrollBarHit.Vertical, hwnd),
            _ => (ScrollBarHit.None, 0),
        };
    }

    private static string GetClassName(nint hwnd)
    {
        var buffer = new char[64];
        int len = InputNative.GetClassNameW(hwnd, buffer, buffer.Length);
        return len > 0 ? new string(buffer, 0, len) : "";
    }
}
