using System.Windows;
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

    private void OnAddProfile(object sender, RoutedEventArgs e)
    {
        Vm?.AddProfile();
        OnEditProfile(sender, e); // 追加直後は名前/対象プロセスを入れるはずなので続けて開く
    }

    private void OnRemoveProfile(object sender, RoutedEventArgs e) => Vm?.RemoveSelectedProfile();

    private void OnEditProfile(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedProfile is not { } item)
            return;
        FlyoutName.Text = item.Model.Name;
        FlyoutPattern.Text = item.IsGlobal ? "(すべてのアプリ)" : item.Model.ProcessPattern;
        FlyoutName.IsEnabled = !item.IsGlobal;
        FlyoutPattern.IsEnabled = !item.IsGlobal;
        FlyoutPickButton.IsEnabled = !item.IsGlobal;
        FlyoutEnabled.IsChecked = item.Model.GesturesEnabled;
        FlyoutError.Visibility = Visibility.Collapsed;
        FlyoutSave.IsEnabled = true;
        ProfileFlyout.IsOpen = true;
        FlyoutName.Focus();
    }

    private void OnFlyoutInputChanged(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedProfile is not { } item)
            return;
        string? error = item.IsGlobal
            ? null
            : GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text, item.IsGlobal);
        FlyoutError.Text = error ?? "";
        FlyoutError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        FlyoutSave.IsEnabled = error is null;
    }

    private void OnFlyoutSave(object sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedProfile: { } item } vm)
            return;
        if (!item.IsGlobal &&
            GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text, item.IsGlobal) is not null)
        {
            return; // 保存ボタンは無効化済みのはずだが念のため
        }
        vm.ApplyProfileEdit(FlyoutName.Text, FlyoutPattern.Text, FlyoutEnabled.IsChecked == true);
        ProfileFlyout.IsOpen = false;
    }

    private void OnFlyoutCancel(object sender, RoutedEventArgs e) => ProfileFlyout.IsOpen = false;

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

    /// <summary>ジェスチャーを無効にするアプリを、実行中アプリの選択ダイアログで設定する。</summary>
    private void OnPickExcludedProcesses(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;
        var dlg = new ProcessPickerDialog(vm.ExcludedProcessesText) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true)
            vm.ExcludedProcessesText = dlg.Result;
    }

    // ── ジェスチャー一覧 ──

    private void OnAddGesture(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;
        var dlg = new GestureEditDialog((Clemotius.Core.Gestures.GestureBinding?)null) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true && dlg.Result is { } binding)
            vm.AddGesture(binding);
    }

    private void OnEditGesture(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || GestureList.SelectedIndex < 0)
            return;
        int index = GestureList.SelectedIndex;
        var current = ((GestureRowViewModel)GestureList.SelectedItem).Binding;
        var dlg = new GestureEditDialog(current) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true && dlg.Result is { } binding)
            vm.UpdateGesture(index, binding);
    }

    private void OnRemoveGesture(object sender, RoutedEventArgs e)
    {
        if (GestureList.SelectedIndex >= 0)
            Vm?.RemoveGestureAt(GestureList.SelectedIndex);
    }

    // ── 右ボタン + ホイール ──

    private void OnEditWheelUp(object sender, RoutedEventArgs e) => EditWheel(up: true);

    private void OnEditWheelDown(object sender, RoutedEventArgs e) => EditWheel(up: false);

    private void EditWheel(bool up)
    {
        if (Vm is not { } vm)
            return;
        string title = up ? "右ボタン + ホイール上" : "右ボタン + ホイール下";
        var dlg = new GestureEditDialog(vm.WheelActionOf(up), title) { Owner = OwnerWindow };
        if (dlg.ShowDialog() == true)
            vm.SetWheelAction(up, dlg.ResultAction);
    }
}
