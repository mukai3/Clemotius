using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Clemotius.Gestures;

/// <summary>
/// ジェスチャー中にカーソル付近へストローク／成立コマンドを表示する小型 WPF オーバーレイ。
/// 試作品 KazaguruR の GestureOverlay を移植。透明・クリック透過・常時最前面。
/// フックスレッドからは直接呼ばず、UI スレッドへマーシャルしてから呼ぶこと。
/// </summary>
internal sealed class GestureCommandOverlay : Window
{
    private readonly TextBlock _text;
    private bool _shownOnce;

    public GestureCommandOverlay()
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
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _text = new TextBlock
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = WpfBrushes.White,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Text = "",
        };
        Content = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(0xCC, 0x1E, 0x1E, 0x2E)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8, 16, 8),
            Child = _text,
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        OverlayNative.MakeClickThrough(new System.Windows.Interop.WindowInteropHelper(this).Handle);
    }

    /// <summary>カーソル付近にテキストを表示する（物理ピクセル座標）。空なら隠す。</summary>
    public void ShowText(string text, int physX, int physY)
    {
        if (string.IsNullOrEmpty(text))
        {
            HideText();
            return;
        }
        _text.Text = text;
        double dpi = OverlayNative.GetDpiScaleAtPoint(physX, physY);
        Left = physX / dpi + 18;
        Top = physY / dpi + 18;
        if (!_shownOnce)
        {
            Show();
            _shownOnce = true;
        }
        else
        {
            Visibility = Visibility.Visible;
        }
    }

    /// <summary>非表示にする（ウィンドウは破棄しない）。</summary>
    public void HideText()
    {
        if (_shownOnce)
            Visibility = Visibility.Collapsed;
    }
}
