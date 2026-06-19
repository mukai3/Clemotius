using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clemotius.SettingsUi.Dialogs;

namespace Clemotius.SettingsUi.Pages;

/// <summary>
/// ジェスチャーページ。プロファイル選択＋一覧表示はバインディング、
/// フライアウト/ダイアログの開閉と確定操作はコードビハインドで行い、
/// 状態の変更はすべて <see cref="GestureViewModel"/> 経由で適用する。
/// </summary>
public partial class GesturePage
{
    public GesturePage()
    {
        InitializeComponent();
        PageDataContext.AttachFallback(this);
    }

    private GestureViewModel? Vm => (DataContext as SettingsViewModel)?.Gesture;

    private Window? OwnerWindow => Window.GetWindow(this);

    // ── プロファイル ──

    // 新規追加フライアウトを開いているか（保存で確定、キャンセルでは何も追加しない）。
    private bool _addingProfile;

    private void OnAddProfile(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;
        // この時点ではまだプロファイルを追加しない。保存されたら確定する（キャンセルで残らないように）。
        _addingProfile = true;
        FlyoutName.Text = vm.SuggestedNewProfileName();
        FlyoutPattern.Text = "";
        FlyoutEnabled.IsChecked = true;
        FlyoutError.Visibility = Visibility.Collapsed;
        // 対象プロセス未入力なので保存は無効から開始
        FlyoutSave.IsEnabled = GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text) is null;
        ProfileEditorOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => FlyoutPattern.Focus()));
    }

    private void OnRemoveProfile(object sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedProfile: { } item } vm)
            return;
        var result = System.Windows.MessageBox.Show(
            OwnerWindow,
            $"プロファイル「{item.Model.Name}」を削除します。よろしいですか？",
            "プロファイルの削除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            vm.RemoveSelectedProfile();
    }

    private void OnEditProfile(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedProfile is not { } item)
            return;
        _addingProfile = false;
        FlyoutName.Text = item.Model.Name;
        FlyoutPattern.Text = item.Model.ProcessPattern;
        FlyoutEnabled.IsChecked = item.Model.GesturesEnabled;
        FlyoutError.Visibility = Visibility.Collapsed;
        FlyoutSave.IsEnabled = true;
        ProfileEditorOverlay.Visibility = Visibility.Visible;
        // Collapsed→Visible 直後はまだ配置前でフォーカスできないため、レイアウト後に設定する
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            FlyoutName.Focus();
            FlyoutName.SelectAll();
        }));
    }

    private void OnFlyoutInputChanged(object sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;
        string? error = GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text);
        FlyoutError.Text = error ?? "";
        FlyoutError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        FlyoutSave.IsEnabled = error is null;
    }

    private void OnFlyoutSave(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;
        if (GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text) is not null)
            return; // 保存ボタンは無効化済みのはずだが念のため
        if (_addingProfile)
            vm.CommitNewProfile(FlyoutName.Text, FlyoutPattern.Text, FlyoutEnabled.IsChecked == true);
        else
            vm.ApplyProfileEdit(FlyoutName.Text, FlyoutPattern.Text, FlyoutEnabled.IsChecked == true);
        _addingProfile = false;
        ProfileEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnFlyoutCancel(object sender, RoutedEventArgs e)
    {
        _addingProfile = false;
        ProfileEditorOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>フライアウトの対象プロセスを、実行中アプリの選択ダイアログで設定する。</summary>
    private void OnPickProfileProcesses(object sender, RoutedEventArgs e)
    {
        var dlg = new ProcessPickerDialog(FlyoutPattern.Text) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true)
        {
            FlyoutPattern.Text = dlg.Result; // TextChanged 経由で検証・保存ボタン状態を更新
            FlyoutPattern.Focus();
        }
    }

    // ── ジェスチャー一覧 ──

    private void OnAddGesture(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;
        var dlg = new GestureEditDialog(null, vm.StrokesInUse()) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true && dlg.Result is { } binding)
            vm.AddGesture(binding);
    }

    private void OnEditGesture(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || GestureList.SelectedIndex < 0)
            return;
        int index = GestureList.SelectedIndex;
        var current = ((GestureRowViewModel)GestureList.SelectedItem).Binding;
        var dlg = new GestureEditDialog(current, vm.StrokesInUse(index)) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true && dlg.Result is { } binding)
            vm.UpdateGesture(index, binding);
    }

    private void OnRemoveGesture(object sender, RoutedEventArgs e)
    {
        if (GestureList.SelectedIndex >= 0)
            Vm?.RemoveGestureAt(GestureList.SelectedIndex);
    }

    /// <summary>選択行が無いときは編集/削除を無効化する（押しても無反応になるのを防ぐ）。</summary>
    private void OnGestureSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = GestureList.SelectedIndex >= 0;
        EditGestureButton.IsEnabled = hasSelection;
        RemoveGestureButton.IsEnabled = hasSelection;
    }
}
