using Clemoutis.Core.Config;
using Wpf.Ui.Appearance;

namespace Clemoutis.SettingsUi;

/// <summary>
/// WPF 版設定ウィンドウ（左ナビ5ページ・即時適用）。
/// 編集状態は <see cref="SettingsViewModel"/> が持ち、変更はデバウンス後に
/// <see cref="SettingsViewModel.Applied"/> 経由で ConfigStore へ保存される。
/// </summary>
public partial class SettingsWindow
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(ClemoutisConfig config)
    {
        ViewModel = new SettingsViewModel(config);
        DataContext = ViewModel;

        InitializeComponent();

        // OS のライト/ダーク切替に追従する
        SystemThemeWatcher.Watch(this);

        // ナビゲーションのページ生成（ページは都度 new でよい軽さなので DataContext を引き継ぐ）
        RootNavigation.Loaded += (_, _) => RootNavigation.Navigate(typeof(Pages.GesturePage));
    }

    /// <summary>常駐側から呼ぶ前面化（最小化解除＋アクティブ化）。</summary>
    public void BringToFront()
    {
        if (WindowState == System.Windows.WindowState.Minimized)
            WindowState = System.Windows.WindowState.Normal;
        Activate();
        bool top = Topmost;
        Topmost = true;
        Topmost = top;
    }
}
