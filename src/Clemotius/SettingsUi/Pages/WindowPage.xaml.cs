namespace Clemotius.SettingsUi.Pages;

/// <summary>ウィンドウページ（タイトルバーアクション＋不透明度）。</summary>
public partial class WindowPage
{
    public WindowPage()
    {
        InitializeComponent();
        PageDataContext.AttachFallback(this);
    }
}
