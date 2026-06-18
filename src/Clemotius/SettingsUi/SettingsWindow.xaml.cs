using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clemotius.Core.Config;

namespace Clemotius.SettingsUi;

/// <summary>
/// WPF 版設定ウィンドウ（左ナビ5ページ・即時適用）。
/// 編集状態は <see cref="SettingsViewModel"/> が持ち、変更はデバウンス後に
/// <see cref="SettingsViewModel.Applied"/> 経由で ConfigStore へ保存される。
/// </summary>
public partial class SettingsWindow
{
    internal SettingsViewModel ViewModel { get; }

    public SettingsWindow(ClemotiusConfig config)
    {
        ViewModel = new SettingsViewModel(config);
        DataContext = ViewModel;

        InitializeComponent();

        // 設定のテーマを適用（system のときだけ OS のライト/ダーク切替に追従）。
        // テーマ選択の変更は即時反映する。
        ThemeApplier.Apply(ViewModel.General.Theme, this);
        // 構築直後の初回適用では一部の DynamicResource（リスト行の文字色など）が旧テーマの
        // ままになり、ダーク直接起動時に黒文字で見えなくなることがある。ロード後に色だけ
        // 再適用して確実にテーマへ追従させる（テーマ切替で直るのと同じ再適用）。
        Loaded += (_, _) => ThemeApplier.ApplyColors(ViewModel.General.Theme);
        ViewModel.General.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GeneralViewModel.ThemeIndex))
                ThemeApplier.Apply(ViewModel.General.Theme, this);
        };

        // ナビゲーションのページ生成（ページは都度 new でよい軽さなので DataContext を引き継ぐ）
        RootNavigation.Loaded += (_, _) => RootNavigation.Navigate(typeof(Pages.GesturePage));

        // ページ内のホイールスクロールをウィンドウ最上位で仲介する。
        // NavigationView の入れ子スクロール領域がホイールを先に消費し、ページの
        // ScrollViewer まで届かないことがあるため、トンネリング（最上位で最初に発火）で
        // カーソル下の実際にスクロール可能な ScrollViewer を探して直接スクロールする。
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0)
            return;
        var sv = FindScrollableViewer(e.OriginalSource as DependencyObject);
        if (sv is null)
            return;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    /// <summary>起点から上方向へ、縦スクロール可能な最初の ScrollViewer を探す。</summary>
    private static ScrollViewer? FindScrollableViewer(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is ScrollViewer sv && sv.ScrollableHeight > 0)
                return sv;
            // Visual は視覚ツリー、それ以外（テキストの Run 等）は論理ツリーを遡る
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
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
