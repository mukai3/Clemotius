namespace Clemotius.SettingsUi.Pages;

/// <summary>拡張スクロールページ（修飾キー6種の動作）。</summary>
public partial class ScrollPage
{
    public ScrollPage()
    {
        InitializeComponent();
        PageDataContext.AttachFallback(this);
    }
}
