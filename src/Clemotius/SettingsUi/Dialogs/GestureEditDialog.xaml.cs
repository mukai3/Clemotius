using System.Windows;
using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>
/// ジェスチャー（ストローク列＋アクション）またはアクション単体を編集する小ダイアログ（WPF版）。
/// ストローク列は U/D/L/R、アクションは キー送信 / プリセット / コマンド。
/// アクションのみモード（右+ホイール割当）ではストローク欄を隠し「割当なし」を許可する。
/// </summary>
public partial class GestureEditDialog
{
    private readonly bool _actionOnly;

    /// <summary>ストローク付き編集の結果。</summary>
    internal GestureBinding? Result { get; private set; }

    /// <summary>アクションのみ編集の結果（クリアした場合は null）。</summary>
    internal GestureAction? ResultAction { get; private set; }

    internal GestureEditDialog(GestureBinding? existing)
    {
        _actionOnly = false;
        InitializeComponent();
        Title = existing is null ? "ジェスチャーの追加" : "ジェスチャーの編集";
        DialogTitleBar.Title = Title;
        Setup();
        if (existing is not null)
        {
            StrokesBox.Text = existing.Strokes;
            LoadAction(existing.Action);
        }
        UpdateParamVisibility();
    }

    internal GestureEditDialog(GestureAction? action, string title)
    {
        _actionOnly = true;
        InitializeComponent();
        Title = title;
        DialogTitleBar.Title = title;
        Setup();
        StrokeRow.Visibility = Visibility.Collapsed;
        ClearButton.Visibility = Visibility.Visible;
        if (action is not null)
            LoadAction(action);
        UpdateParamVisibility();
    }

    private void Setup()
    {
        foreach (var name in ActionDisplay.TypeNames)
            TypeCombo.Items.Add(name);
        TypeCombo.SelectedItem = ActionDisplay.TypeKey;

        foreach (var name in Enum.GetNames<AppCommand>())
            CommandCombo.Items.Add(name);
        PresetCombo.ItemsSource = PresetCommands.All;
    }

    private void LoadAction(GestureAction action)
    {
        TypeCombo.SelectedItem = ActionDisplay.TypeNameOf(action);
        switch (action)
        {
            case KeyAction { Label: { } label } k:
                // プリセット由来: 名前一致するプリセットを選択（カタログから消えていたらキー送信扱い）
                var match = PresetCommands.All.FirstOrDefault(p => p.Display == label);
                if (match is not null)
                {
                    PresetCombo.SelectedItem = match;
                }
                else
                {
                    TypeCombo.SelectedItem = ActionDisplay.TypeKey;
                    KeysBox.Stroke = k.Stroke;
                }
                break;
            case KeyAction k:
                KeysBox.Stroke = k.Stroke;
                break;
            case AppCommandAction c:
                CommandCombo.SelectedItem = c.Command.ToString();
                break;
        }
    }

    private void OnTypeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateParamVisibility();

    private void UpdateParamVisibility()
    {
        string type = TypeCombo.SelectedItem as string ?? ActionDisplay.TypeKey;
        bool isKey = type == ActionDisplay.TypeKey;
        bool isCmd = type == ActionDisplay.TypeAppCommand;
        bool isPreset = type == ActionDisplay.TypePreset;
        KeysBox.Visibility = isKey ? Visibility.Visible : Visibility.Collapsed;
        CommandCombo.Visibility = isCmd ? Visibility.Visible : Visibility.Collapsed;
        PresetCombo.Visibility = isPreset ? Visibility.Visible : Visibility.Collapsed;
        ParamLabel.Text = isKey ? "キー(クリックして押す)" : isCmd ? "コマンド" : "プリセット";
        if (isCmd && CommandCombo.SelectedIndex < 0 && CommandCombo.Items.Count > 0)
            CommandCombo.SelectedIndex = 0;
    }

    private void OnCaptureStroke(object sender, RoutedEventArgs e)
    {
        var dlg = new StrokeCaptureDialog(StrokesBox.Text.Trim()) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } strokes)
            StrokesBox.Text = strokes;
    }

    private void OnClearAssignment(object sender, RoutedEventArgs e)
    {
        ResultAction = null;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!_actionOnly)
        {
            string strokes = StrokesBox.Text.Trim();
            if (!IsValidStrokes(strokes))
            {
                Warn("ストロークは U/D/L/R の組み合わせで入力してください(例: DR)。");
                return;
            }
            if (!TryBuildAction(out var act))
                return;
            Result = new GestureBinding(strokes, act!);
        }
        else
        {
            if (!TryBuildAction(out var action))
                return;
            ResultAction = action;
        }
        DialogResult = true;
        Close();
    }

    private bool TryBuildAction(out GestureAction? action)
    {
        action = null;
        string type = TypeCombo.SelectedItem as string ?? ActionDisplay.TypeKey;
        if (type == ActionDisplay.TypeKey)
        {
            if (KeysBox.Stroke is not { } stroke)
            {
                Warn("キーを押して設定してください。");
                return false;
            }
            action = new KeyAction(stroke);
        }
        else if (type == ActionDisplay.TypePreset)
        {
            if (PresetCombo.SelectedItem is not PresetCommand preset)
            {
                Warn("プリセットを選択してください。");
                return false;
            }
            // プリセットはキー送信に展開し、表示用にプリセット名を保持する
            action = new KeyAction(preset.Stroke, preset.Display);
        }
        else if (type == ActionDisplay.TypeAppCommand)
        {
            if (CommandCombo.SelectedItem is not string name || !Enum.TryParse<AppCommand>(name, out var cmd))
            {
                Warn("コマンドを選択してください。");
                return false;
            }
            action = new AppCommandAction(cmd);
        }
        else
        {
            action = new CloseAction();
        }
        return true;
    }

    private static bool IsValidStrokes(string s) =>
        s.Length > 0 && s.All(c => c is 'U' or 'D' or 'L' or 'R');

    private void Warn(string message)
        => System.Windows.MessageBox.Show(
            this, message, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
}
