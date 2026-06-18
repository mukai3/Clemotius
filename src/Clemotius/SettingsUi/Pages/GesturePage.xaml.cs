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
        if (Vm?.SelectedProfile is null)
            return;
        string? error = GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text);
        FlyoutError.Text = error ?? "";
        FlyoutError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        FlyoutSave.IsEnabled = error is null;
    }

    private void OnFlyoutSave(object sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedProfile: not null } vm)
            return;
        if (GestureViewModel.ValidateProfileEdit(FlyoutName.Text, FlyoutPattern.Text) is not null)
            return; // 保存ボタンは無効化済みのはずだが念のため
        vm.ApplyProfileEdit(FlyoutName.Text, FlyoutPattern.Text, FlyoutEnabled.IsChecked == true);
        ProfileEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnFlyoutCancel(object sender, RoutedEventArgs e)
        => ProfileEditorOverlay.Visibility = Visibility.Collapsed;

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

    /// <summary>選択行が無いときは編集/削除を無効化する（押しても無反応になるのを防ぐ）。</summary>
    private void OnGestureSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = GestureList.SelectedIndex >= 0;
        EditGestureButton.IsEnabled = hasSelection;
        RemoveGestureButton.IsEnabled = hasSelection;
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
