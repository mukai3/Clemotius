using System.Windows;
using System.Windows.Controls;
using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>
/// ジェスチャー（ストローク列＋アクション）を編集する小ダイアログ（WPF版）。
/// ストロークは U/D/L/R の軌跡、または右ボタン+ホイールを表す WU/WD（単独・他と混在不可）。
/// アクションは キー送信 / プリセット / コマンド。
/// </summary>
public partial class GestureEditDialog
{
    private readonly IReadOnlyCollection<string> _existingStrokes;

    /// <summary>編集結果（OK 時のみ非 null）。</summary>
    internal GestureBinding? Result { get; private set; }

    /// <param name="existing">編集対象。新規追加なら null。</param>
    /// <param name="existingStrokes">同プロファイルで既に使用中のストローク（編集対象自身は除く）。重複防止に使う。</param>
    internal GestureEditDialog(GestureBinding? existing, IReadOnlyCollection<string> existingStrokes)
    {
        _existingStrokes = existingStrokes;
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
        ContentRendered += OnContentRenderedLockSize;
    }

    private bool _sizeLocked;

    // SizeToContent="Height" が内容に合わせて確定した後の実寸で Min=Max を固定し、リサイズを防ぐ。
    // SizeToContent の解除や Height の明示設定はしない（FluentWindow では ExtendsContentIntoTitleBar
    // と相まってキャプション分の下部余白が生じるため）。Min=Max なので NoResize が効かなくても固定される。
    private void OnContentRenderedLockSize(object? sender, EventArgs e)
    {
        if (_sizeLocked)
            return;
        _sizeLocked = true;
        MinHeight = MaxHeight = ActualHeight;
        MinWidth = MaxWidth = ActualWidth;
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

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e) => UpdateParamVisibility();

    private void UpdateParamVisibility()
    {
        string type = TypeCombo.SelectedItem as string ?? ActionDisplay.TypeKey;
        bool isKey = type == ActionDisplay.TypeKey;
        bool isCmd = type == ActionDisplay.TypeAppCommand;
        bool isPreset = type == ActionDisplay.TypePreset;
        KeysBox.Visibility = isKey ? Visibility.Visible : Visibility.Collapsed;
        CommandCombo.Visibility = isCmd ? Visibility.Visible : Visibility.Collapsed;
        PresetCombo.Visibility = isPreset ? Visibility.Visible : Visibility.Collapsed;
        ParamLabel.Text = isKey ? "キー" : isCmd ? "コマンド" : "プリセット";
        if (isCmd && CommandCombo.SelectedIndex < 0 && CommandCombo.Items.Count > 0)
            CommandCombo.SelectedIndex = 0;
    }

    private void OnCaptureStroke(object sender, RoutedEventArgs e)
    {
        var dlg = new StrokeCaptureDialog(StrokesBox.Text.Trim()) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } strokes)
            StrokesBox.Text = strokes;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        string strokes = StrokesBox.Text.Trim().ToUpperInvariant();
        if (!IsValidStrokes(strokes))
        {
            Warn("ストロークは U/D/L/R の組み合わせ、またはホイールの WU/WD（単独）で入力してください。");
            return;
        }
        if (_existingStrokes.Contains(strokes))
        {
            Warn("このストロークは既に登録されています。");
            return;
        }
        if (!TryBuildAction(out var act))
            return;
        Result = new GestureBinding(strokes, act!);
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

    /// <summary>U/D/L/R の組み合わせ、または WU/WD（ホイール・単独）のみ有効。混在は不可。</summary>
    private static bool IsValidStrokes(string s)
    {
        if (WheelStrokes.IsWheel(s))
            return true;
        return s.Length > 0 && s.All(c => c is 'U' or 'D' or 'L' or 'R');
    }

    private void Warn(string message)
        => System.Windows.MessageBox.Show(
            this, message, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
}
