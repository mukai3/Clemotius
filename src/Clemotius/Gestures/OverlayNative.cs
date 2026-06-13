using System.Runtime.InteropServices;
using WpfColor = System.Windows.Media.Color;

namespace Clemotius.Gestures;

/// <summary>WPF オーバーレイ共通の Win32 / DPI / 色ヘルパー。</summary>
internal static class OverlayNative
{
    /// <summary>ウィンドウをクリック透過・非アクティブ化・ツールウィンドウ化する。</summary>
    public static void MakeClickThrough(nint hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>指定物理座標のモニターの DPI スケール（物理px / 論理px）。取得不可なら 1.0。</summary>
    public static double GetDpiScaleAtPoint(int x, int y)
    {
        nint mon = MonitorFromPoint(new POINT { x = x, y = y }, MONITOR_DEFAULTTONEAREST);
        if (mon == 0)
            return 1.0;
        if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint dpiX, out _) != 0)
            return 1.0;
        return dpiX / 96.0;
    }

    public static WpfColor ParseColor(string hex)
    {
        try
        {
            string s = hex.TrimStart('#');
            if (s.Length == 6)
                return WpfColor.FromRgb(
                    Convert.ToByte(s[..2], 16),
                    Convert.ToByte(s[2..4], 16),
                    Convert.ToByte(s[4..6], 16));
        }
        catch (FormatException) { }
        return WpfColor.FromRgb(0x80, 0xFF, 0x00);
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint MDT_EFFECTIVE_DPI = 0;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x80;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
