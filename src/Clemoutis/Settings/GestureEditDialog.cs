using Clemoutis.Core.Actions;
using Clemoutis.Core.Gestures;

namespace Clemoutis.Settings;

/// <summary>
/// ジェスチャー（ストローク列＋アクション）またはアクション単体を編集する小ダイアログ。
/// ストローク列は U/D/L/R、アクションは キー送信 / コマンド / 閉じる。
/// アクションのみモード（右+ホイール割当）ではストローク欄を隠す。
/// </summary>
internal sealed class GestureEditDialog : Form
{
    private readonly bool _actionOnly;
    private readonly TextBox _strokes = new() { CharacterCasing = CharacterCasing.Upper };
    private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KeyCaptureBox _keys = new();
    private readonly ComboBox _command = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _paramLabel = new();

    /// <summary>ストローク付き編集の結果。</summary>
    public GestureBinding? Result { get; private set; }

    /// <summary>アクションのみ編集の結果（クリアした場合は null）。</summary>
    public GestureAction? ResultAction { get; private set; }

    public GestureEditDialog(GestureBinding? existing)
    {
        _actionOnly = false;
        Text = existing is null ? "ジェスチャーの追加" : "ジェスチャーの編集";
        BuildLayout();
        if (existing is not null)
        {
            _strokes.Text = existing.Strokes;
            LoadAction(existing.Action);
        }
        else
        {
            _type.SelectedItem = ActionDisplay.TypeKey;
        }
        UpdateParamVisibility();
    }

    public GestureEditDialog(GestureAction? action, string title)
    {
        _actionOnly = true;
        Text = title;
        BuildLayout();
        if (action is not null)
            LoadAction(action);
        else
            _type.SelectedItem = ActionDisplay.TypeKey;
        UpdateParamVisibility();
    }

    private void BuildLayout()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        int top = 12;
        var controls = new List<Control>();

        if (!_actionOnly)
        {
            controls.Add(new Label { Text = "ストローク (U/D/L/R)", Left = 12, Top = top + 3, Width = 140 });
            _strokes.SetBounds(160, top, 180, 23);
            controls.Add(_strokes);
            top += 33;
        }

        controls.Add(new Label { Text = "アクション種別", Left = 12, Top = top + 3, Width = 140 });
        _type.SetBounds(160, top, 180, 23);
        _type.Items.AddRange(ActionDisplay.TypeNames);
        _type.SelectedIndexChanged += (_, _) => UpdateParamVisibility();
        controls.Add(_type);
        top += 33;

        _paramLabel.SetBounds(12, top + 3, 140, 23);
        _keys.SetBounds(160, top, 180, 23);
        _command.SetBounds(160, top, 180, 23);
        _command.Items.AddRange(Enum.GetNames<AppCommand>());
        controls.Add(_paramLabel);
        controls.Add(_keys);
        controls.Add(_command);
        top += 45;

        var ok = new Button { Text = "OK", Left = 174, Top = top, Width = 80 };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Left = 260, Top = top, Width = 80 };
        ok.Click += OnOk;
        controls.Add(ok);
        controls.Add(cancel);

        // アクションのみモードでは「割当なし(クリア)」を許可
        if (_actionOnly)
        {
            var clear = new Button { Text = "割当なし", Left = 12, Top = top, Width = 90 };
            clear.Click += (_, _) => { ResultAction = null; DialogResult = DialogResult.OK; };
            controls.Add(clear);
        }

        ClientSize = new Size(360, top + 38);
        Controls.AddRange(controls.ToArray());
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void LoadAction(GestureAction action)
    {
        _type.SelectedItem = ActionDisplay.TypeNameOf(action);
        switch (action)
        {
            case KeyAction k:
                _keys.Stroke = k.Stroke;
                break;
            case AppCommandAction c:
                _command.SelectedItem = c.Command.ToString();
                break;
        }
    }

    private void UpdateParamVisibility()
    {
        string type = (string?)_type.SelectedItem ?? ActionDisplay.TypeKey;
        bool isKey = type == ActionDisplay.TypeKey;
        bool isCmd = type == ActionDisplay.TypeAppCommand;
        _keys.Visible = isKey;
        _command.Visible = isCmd;
        _paramLabel.Visible = isKey || isCmd;
        _paramLabel.Text = isKey ? "キー（クリックして押す）" : isCmd ? "コマンド" : "";
        if (isCmd && _command.SelectedIndex < 0 && _command.Items.Count > 0)
            _command.SelectedIndex = 0;
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (!_actionOnly)
        {
            string strokes = _strokes.Text.Trim();
            if (!IsValidStrokes(strokes))
            {
                Warn("ストロークは U/D/L/R の組み合わせで入力してください（例: DR）。");
                return;
            }
            if (!TryBuildAction(out var act))
                return;
            Result = new GestureBinding(strokes, act!);
            DialogResult = DialogResult.OK;
            return;
        }

        if (!TryBuildAction(out var action))
            return;
        ResultAction = action;
        DialogResult = DialogResult.OK;
    }

    private bool TryBuildAction(out GestureAction? action)
    {
        action = null;
        string type = (string?)_type.SelectedItem ?? ActionDisplay.TypeKey;
        if (type == ActionDisplay.TypeKey)
        {
            if (_keys.Stroke is not { } stroke)
            {
                Warn("キーを押して設定してください。");
                return false;
            }
            action = new KeyAction(stroke);
        }
        else if (type == ActionDisplay.TypeAppCommand)
        {
            if (_command.SelectedItem is not string name || !Enum.TryParse<AppCommand>(name, out var cmd))
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
    {
        MessageBox.Show(this, message, "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        DialogResult = DialogResult.None;
    }
}
