using System.Windows;

namespace Clemotius.SettingsUi.Pages;

/// <summary>
/// NavigationView が生成するページに SettingsViewModel を確実に届けるためのヘルパー。
/// 通常は WPF の DataContext 継承で足りるが、コンテンツプレゼンター側の実装に
/// 依存しないよう、未設定ならウィンドウから明示的に引き継ぐ。
/// </summary>
internal static class PageDataContext
{
    public static void AttachFallback(FrameworkElement page)
    {
        page.Loaded += (_, _) =>
        {
            if (page.DataContext is null && Window.GetWindow(page) is SettingsWindow w)
                page.DataContext = w.ViewModel;
        };
    }
}
