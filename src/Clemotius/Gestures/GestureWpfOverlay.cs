using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clemotius.Core.Config;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Clemotius.Gestures;

/// <summary>
/// ジェスチャー軌跡を描画する WPF オーバーレイ（透明・クリック透過・常時最前面）。
/// WPF の保持モード描画により Hide/Show を繰り返さなくても更新が確実に反映される
/// （WinForms レイヤード方式で軌跡が描画されない問題への対処）。試作品 KazaguruR を移植。
/// フックスレッドからは直接呼ばず、UI スレッドへマーシャルしてから呼ぶこと。
/// </summary>
internal sealed class GestureWpfOverlay : Window
{
    private readonly Canvas _canvas = new();
    private Polyline? _line;
    private double _dpiScale = 1.0;
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
        Content = _canvas;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        OverlayNative.MakeClickThrough(new System.Windows.Interop.WindowInteropHelper(this).Handle);
    }

    public void ApplySettings(GestureSettings g)
    {
        _strokeWidth = Math.Max(1, g.StrokeWidth);
        _strokeColor = OverlayNative.ParseColor(g.ValidStrokeColor);
    }

    /// <summary>ジェスチャー開始。全画面に広げて軌跡をリセットする（物理ピクセル座標）。</summary>
    public void Begin(int physX, int physY)
    {
        _dpiScale = OverlayNative.GetDpiScaleAtPoint(physX, physY);
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        if (_line is not null)
            _canvas.Children.Remove(_line);
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

        // 初回のみ表示。以後は隠さない（Hide/Show による前フレーム残りを避ける）。
        if (!_shownOnce)
        {
            Show();
            _shownOnce = true;
        }
    }

    public void AddPoint(int physX, int physY) => _line?.Points.Add(ToCanvas(physX, physY));

    /// <summary>ジェスチャー終了。軌跡をクリアするのみ（ウィンドウは隠さない）。</summary>
    public void End()
    {
        if (_line is not null)
        {
            _canvas.Children.Remove(_line);
            _line = null;
        }
    }

    private WpfPoint ToCanvas(int physX, int physY)
        => new(physX / _dpiScale - Left, physY / _dpiScale - Top);
}
