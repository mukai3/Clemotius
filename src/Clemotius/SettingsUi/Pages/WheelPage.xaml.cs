namespace Clemotius.SettingsUi.Pages;

/// <summary>ホイールページ（スクロールバー上での動作）。</summary>
public partial class WheelPage
{
    public WheelPage()
    {
        InitializeComponent();
        PageDataContext.AttachFallback(this);
    }
}
