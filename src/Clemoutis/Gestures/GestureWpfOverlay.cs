using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clemoutis.Core.Config;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;
using WpfBrushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Clemoutis.Gestures;

/// <summary>
/// ジェスチャーの軌跡／コマンドを描画する WPF オーバーレイ（透明・クリック透過・常時最前面）。
/// WPF の保持モード描画により、Hide/Show を繰り返さなくても内容更新が確実に反映される
/// （WinForms レイヤード方式で軌跡が描画されない問題への対処）。試作品 KazaguruR の方式を移植。
///
/// 注: フックスレッドからは直接呼ばず、UI スレッドへマーシャルしてから呼ぶこと。
/// </summary>
internal sealed class GestureWpfOverlay : Window
{
    internal enum DrawMode { Trail, Command }

    private readonly Canvas _canvas = new();
    private readonly Border _commandBox;
    private readonly TextBlock _commandText;
    private Polyline? _line;
    private double _dpiScale = 1.0;

    private DrawMode _mode = DrawMode.Trail;
    private WpfColor _strokeColor = WpfColor.FromRgb(0x80, 0xFF, 0x00);
    private double _strokeWidth = 2;
    private bool _shownOnce;

    public GestureWpfOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = WpfBrushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _commandText = new TextBlock
        {
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = WpfBrushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "",
        };
        _commandBox = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(0xCC, 0x20, 0x20, 0x2E)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8, 16, 8),
            Child = _commandText,
            Visibility = Visibility.Collapsed,
        };
        _canvas.Children.Add(_commandBox);
        Content = _canvas;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    public void ApplySettings(GestureSettings g)
    {
        _strokeWidth = Math.Max(1, g.StrokeWidth);
        _strokeColor = ParseColor(g.ValidStrokeColor);
        _mode = g.DrawingType == 1 ? DrawMode.Command : DrawMode.Trail;
    }

    /// <summary>ジェスチャー開始。全画面に広げて内容をリセットする（物理ピクセル座標）。</summary>
    public void Begin(int physX, int physY)
    {
        _dpiScale = GetDpiScaleAtPoint(physX, physY);

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        // 軌跡をリセット
        if (_line is not null)
            _canvas.Children.Remove(_line);
        _line = null;
        _commandText.Text = "";
        _commandBox.Visibility = Visibility.Collapsed;

        if (_mode == DrawMode.Trail)
        {
            _line = new Polyline
            {
                Stroke = new SolidColorBrush(_strokeColor),
                StrokeThickness = _strokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            _canvas.Children.Add(_line);
            _line.Points.Add(ToCanvas(physX, physY));
        }

        // 初回のみ表示。以後は隠さない（Hide/Show による前フレーム残りを避ける）。
        if (!_shownOnce)
        {
            Show();
            _shownOnce = true;
        }
    }

    public void AddPoint(int physX, int physY)
    {
        _line?.Points.Add(ToCanvas(physX, physY));
    }

    /// <summary>コマンド表示モードの表示テキストを更新する。</summary>
    public void SetCommand(string text)
    {
        if (_mode != DrawMode.Command)
            return;
        _commandText.Text = text;
        _commandBox.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        // 画面上部中央に配置
        _commandBox.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        double boxW = _commandBox.DesiredSize.Width;
        Canvas.SetLeft(_commandBox, Math.Max(0, (Width - boxW) / 2));
        Canvas.SetTop(_commandBox, Height * 0.12);
    }

    /// <summary>ジェスチャー終了。内容をクリアするのみ（ウィンドウは隠さない）。</summary>
    public void End()
    {
        if (_line is not null)
        {
            _canvas.Children.Remove(_line);
            _line = null;
        }
        _commandText.Text = "";
        _commandBox.Visibility = Visibility.Collapsed;
    }

    // 物理ピクセル → WPF 論理座標 → キャンバス内ローカル座標
    private WpfPoint ToCanvas(int physX, int physY)
        => new(physX / _dpiScale - Left, physY / _dpiScale - Top);

    private static WpfColor ParseColor(string hex)
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

    // ── DPI / Win32 ──
    private static double GetDpiScaleAtPoint(int x, int y)
    {
        nint mon = MonitorFromPoint(new POINT { x = x, y = y }, MONITOR_DEFAULTTONEAREST);
        if (mon == 0)
            return 1.0;
        if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint dpiX, out _) != 0)
            return 1.0;
        return dpiX / 96.0;
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
