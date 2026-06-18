using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;

namespace Clemotius.SettingsUi.Pages;

/// <summary>アプリのタイトル・バージョン・クレジットを表示するページ。</summary>
public partial class AboutPage
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = ResolveVersion() is { Length: > 0 } v ? $"version {v}" : "";
    }

    // MinVer が git タグから設定する InformationalVersion を表示に使う(例 "0.3.4")。
    // タグ未一致の開発ビルドは "0.3.5-alpha.0.5+<sha>" の形になるため、"+" 以降の
    // ビルドメタデータ(コミットハッシュ)は表示では落とす。
    private static string ResolveVersion()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info))
            return "";
        int plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
