using System.Globalization;
using Clemoutis.Core.Config;

namespace Clemoutis.Settings;

/// <summary>
/// オリジナルの設定画面をベースにしたプロパティシート。v1 スコープの4タブのみ:
/// マウスジェスチャー / 拡張スクロール / ホイール / 一般・詳細。
/// 編集結果は OK/適用で <see cref="Applied"/> 経由（ConfigStore へ保存）。
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly ClemoutisConfig _original;
    private readonly List<MutableProfile> _profiles;

    // --- マウスジェスチャー タブ ---
    private readonly ComboBox _profileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _profilePattern = new();
    private readonly CheckBox _gesturesEnabled = new() { Text = "このプロファイルでジェスチャーを有効にする" };
    private readonly ListView _gestureList = new();
    private readonly Label _wheelUpLabel = new();
    private readonly Label _wheelDownLabel = new();

    // --- 拡張スクロール タブ ---
    private readonly Dictionary<string, ComboBox> _modifierCombos = new();

    // --- ホイール タブ ---
    private readonly ComboBox _onVerticalScrollbar = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _onHorizontalScrollbar = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // --- 一般・詳細 タブ ---
    private readonly CheckBox _showTrayIcon = new() { Text = "タスクトレイにアイコンを表示する" };
    private readonly CheckBox _showBalloonTip = new() { Text = "バルーン通知を表示する" };
    private readonly NumericUpDown _range = new() { Minimum = 1, Maximum = 100 };
    private readonly NumericUpDown _timeout = new() { Minimum = 0, Maximum = 10000, Increment = 100 };
    private readonly NumericUpDown _pushHold = new() { Minimum = 0, Maximum = 5000, Increment = 50 };
    private readonly CheckBox _drawStroke = new() { Text = "ジェスチャーの軌跡を描画する" };
    private readonly NumericUpDown _strokeWidth = new() { Minimum = 1, Maximum = 20 };
    private readonly Button _validColor = new() { Text = "有効色" };
    private readonly Button _invalidColor = new() { Text = "無効色" };
    private Color _validColorValue;
    private Color _invalidColorValue;

    /// <summary>OK/適用で発火。新しい設定を渡す。</summary>
    public event Action<ClemoutisConfig>? Applied;

    public SettingsForm(ClemoutisConfig config)
    {
        _original = config;
        _profiles = config.Profiles.Select(MutableProfile.From).ToList();

        Text = "Clemoutis 設定";
        Icon = AppIcon.Shared;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 485);

        var tabs = new TabControl { Dock = DockStyle.Top, Height = 435 };
        tabs.TabPages.Add(BuildGestureTab());
        tabs.TabPages.Add(BuildScrollTab());
        tabs.TabPages.Add(BuildWheelTab());
        tabs.TabPages.Add(BuildGeneralTab());

        var ok = new Button { Text = "OK", Width = 90, Left = 230, Top = 447 };
        var cancel = new Button { Text = "キャンセル", Width = 90, Left = 326, Top = 447 };
        var apply = new Button { Text = "適用", Width = 90, Left = 422, Top = 447 };
        ok.Click += (_, _) => { if (ApplyChanges()) { DialogResult = DialogResult.OK; Close(); } };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        apply.Click += (_, _) => ApplyChanges();

        Controls.Add(tabs);
        Controls.AddRange(new Control[] { ok, cancel, apply });
        AcceptButton = ok;
        CancelButton = cancel;

        LoadFromConfig();
    }

    // ---------------------------------------------------------------- ジェスチャー
    private TabPage BuildGestureTab()
    {
        var page = new TabPage("マウス ジェスチャー");

        _profileCombo.SetBounds(12, 14, 240, 23);
        _profileCombo.SelectedIndexChanged += (_, _) => OnProfileSelected();
        var addProfile = new Button { Text = "プロファイル追加", Left = 262, Top = 13, Width = 110 };
        var removeProfile = new Button { Text = "削除", Left = 378, Top = 13, Width = 70 };
        addProfile.Click += (_, _) => AddProfile();
        removeProfile.Click += (_, _) => RemoveProfile();

        var patternLabel = new Label { Text = "対象プロセス名（* で全て）", Left = 12, Top = 46, Width = 170 };
        _profilePattern.SetBounds(186, 43, 200, 23);
        _profilePattern.Leave += (_, _) => SaveProfileHeader();
        _gesturesEnabled.SetBounds(12, 72, 380, 23);
        _gesturesEnabled.CheckedChanged += (_, _) => SaveProfileHeader();

        _gestureList.SetBounds(12, 102, 436, 180);
        _gestureList.View = View.Details;
        _gestureList.FullRowSelect = true;
        _gestureList.MultiSelect = false;
        _gestureList.Columns.Add("ストローク", 110);
        _gestureList.Columns.Add("アクション", 310);
        _gestureList.DoubleClick += (_, _) => EditGesture();

        var add = new Button { Text = "追加", Left = 12, Top = 288, Width = 80 };
        var edit = new Button { Text = "編集", Left = 98, Top = 288, Width = 80 };
        var remove = new Button { Text = "削除", Left = 184, Top = 288, Width = 80 };
        add.Click += (_, _) => AddGesture();
        edit.Click += (_, _) => EditGesture();
        remove.Click += (_, _) => RemoveGesture();

        var wheelGroup = new GroupBox { Text = "右ボタン + ホイール" };
        wheelGroup.SetBounds(12, 320, 436, 90);
        var wheelUpBtn = new Button { Text = "上を設定...", Left = 8, Top = 22, Width = 90 };
        var wheelDownBtn = new Button { Text = "下を設定...", Left = 8, Top = 54, Width = 90 };
        wheelUpBtn.Click += (_, _) => EditWheelAction(up: true);
        wheelDownBtn.Click += (_, _) => EditWheelAction(up: false);
        _wheelUpLabel.SetBounds(106, 26, 320, 20);
        _wheelDownLabel.SetBounds(106, 58, 320, 20);
        _wheelUpLabel.AutoEllipsis = true;
        _wheelDownLabel.AutoEllipsis = true;
        wheelGroup.Controls.AddRange(new Control[] { wheelUpBtn, _wheelUpLabel, wheelDownBtn, _wheelDownLabel });

        page.Controls.AddRange(new Control[]
        {
            _profileCombo, addProfile, removeProfile, patternLabel, _profilePattern,
            _gesturesEnabled, _gestureList, add, edit, remove, wheelGroup,
        });
        return page;
    }

    private void EditWheelAction(bool up)
    {
        if (Selected is not { } p)
            return;
        var current = up ? p.WheelUp : p.WheelDown;
        string title = up ? "右ボタン + ホイール上" : "右ボタン + ホイール下";
        using var dlg = new GestureEditDialog(current, title);
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        if (up) p.WheelUp = dlg.ResultAction;
        else p.WheelDown = dlg.ResultAction;
        RefreshWheelLabels();
    }

    private void RefreshWheelLabels()
    {
        _wheelUpLabel.Text = Selected?.WheelUp is { } u ? ActionDisplay.Describe(u) : "（なし）";
        _wheelDownLabel.Text = Selected?.WheelDown is { } d ? ActionDisplay.Describe(d) : "（なし）";
    }

    // ---------------------------------------------------------------- 拡張スクロール
    private TabPage BuildScrollTab()
    {
        var page = new TabPage("拡張スクロール");
        var group = new GroupBox { Text = "修飾キーを押しているときの動作" };
        group.SetBounds(12, 12, 480, 230);

        var combos = new (string key, string label)[]
        {
            ("Shift", "Shift + ホイール"),
            ("Ctrl", "Ctrl + ホイール"),
            ("CtrlShift", "Ctrl + Shift + ホイール"),
            ("Alt", "Alt + ホイール"),
            ("ShiftAlt", "Shift + Alt + ホイール"),
            ("CtrlAlt", "Ctrl + Alt + ホイール"),
        };
        int top = 28;
        foreach (var (key, label) in combos)
        {
            group.Controls.Add(new Label { Text = label, Left = 16, Top = top + 3, Width = 200 });
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.SetBounds(230, top, 220, 23);
            combo.Format += (s, e) => e.Value = ScrollBehaviorChoice.Display((string)e.ListItem!);
            _modifierCombos[key] = combo;
            group.Controls.Add(combo);
            top += 32;
        }

        page.Controls.Add(group);
        return page;
    }

    // ---------------------------------------------------------------- ホイール
    private TabPage BuildWheelTab()
    {
        var page = new TabPage("ホイール");
        var group = new GroupBox { Text = "スクロールバー上での動作" };
        group.SetBounds(12, 12, 480, 100);

        group.Controls.Add(new Label { Text = "垂直スクロールバー上でホイール回転", Left = 16, Top = 31, Width = 230 });
        _onVerticalScrollbar.SetBounds(250, 28, 200, 23);
        _onVerticalScrollbar.Format += (s, e) => e.Value = ScrollBehaviorChoice.Display((string)e.ListItem!);
        group.Controls.Add(new Label { Text = "水平スクロールバー上でホイール回転", Left = 16, Top = 63, Width = 230 });
        _onHorizontalScrollbar.SetBounds(250, 60, 200, 23);
        _onHorizontalScrollbar.Format += (s, e) => e.Value = ScrollBehaviorChoice.Display((string)e.ListItem!);
        group.Controls.AddRange(new Control[] { _onVerticalScrollbar, _onHorizontalScrollbar });

        var note = new Label
        {
            Text = "※ カーソル下を常にスクロール・前面化・フォーカス合わせ・スクロール加速は本バージョンでは未実装です。",
            Left = 12, Top = 122, Width = 480, Height = 40,
        };
        page.Controls.AddRange(new Control[] { group, note });
        return page;
    }

    // ---------------------------------------------------------------- 一般・詳細
    private TabPage BuildGeneralTab()
    {
        var page = new TabPage("一般・詳細");

        var trayGroup = new GroupBox { Text = "トレイ" };
        trayGroup.SetBounds(12, 12, 480, 80);
        _showTrayIcon.SetBounds(16, 24, 440, 23);
        _showBalloonTip.SetBounds(16, 50, 440, 23);
        trayGroup.Controls.AddRange(new Control[] { _showTrayIcon, _showBalloonTip });

        var gestureGroup = new GroupBox { Text = "ジェスチャーの詳細" };
        gestureGroup.SetBounds(12, 100, 480, 200);

        gestureGroup.Controls.Add(new Label { Text = "1 ストロークの距離 (px)", Left = 16, Top = 30, Width = 160 });
        _range.SetBounds(180, 27, 80, 23);
        gestureGroup.Controls.Add(new Label { Text = "入力開始のタイムアウト (ms)", Left = 16, Top = 60, Width = 160 });
        _timeout.SetBounds(180, 57, 80, 23);
        gestureGroup.Controls.Add(new Label { Text = "長押しの待ち時間 (ms)", Left = 16, Top = 90, Width = 160 });
        _pushHold.SetBounds(180, 87, 80, 23);

        _drawStroke.SetBounds(16, 120, 300, 23);
        gestureGroup.Controls.Add(new Label { Text = "幅 (px)", Left = 16, Top = 152, Width = 150 });
        _strokeWidth.SetBounds(180, 149, 80, 23);
        _validColor.SetBounds(280, 147, 90, 27);
        _invalidColor.SetBounds(376, 147, 90, 27);
        _validColor.Click += (_, _) => PickColor(ref _validColorValue, _validColor);
        _invalidColor.Click += (_, _) => PickColor(ref _invalidColorValue, _invalidColor);

        gestureGroup.Controls.AddRange(new Control[]
        {
            _range, _timeout, _pushHold, _drawStroke, _strokeWidth, _validColor, _invalidColor,
        });

        page.Controls.AddRange(new Control[] { trayGroup, gestureGroup });
        return page;
    }

    // ---------------------------------------------------------------- ロード/保存
    private void LoadFromConfig()
    {
        RefreshProfileCombo();
        if (_profileCombo.Items.Count > 0)
            _profileCombo.SelectedIndex = 0;

        var ms = _original.Scroll.ModifierScroll;
        SetCombo("Shift", ms.Shift);
        SetCombo("Ctrl", ms.Ctrl);
        SetCombo("CtrlShift", ms.CtrlShift);
        SetCombo("Alt", ms.Alt);
        SetCombo("ShiftAlt", ms.ShiftAlt);
        SetCombo("CtrlAlt", ms.CtrlAlt);

        SetScrollbarCombo(_onVerticalScrollbar, _original.Scroll.OnVerticalScrollbar);
        SetScrollbarCombo(_onHorizontalScrollbar, _original.Scroll.OnHorizontalScrollbar);

        _showTrayIcon.Checked = _original.Tray.ShowTrayIcon;
        _showBalloonTip.Checked = _original.Tray.ShowBalloonTip;
        _range.Value = Clamp(_original.Gesture.Range, _range);
        _timeout.Value = Clamp(_original.Gesture.TimeoutMs, _timeout);
        _pushHold.Value = Clamp(_original.Gesture.PushHoldTimeMs, _pushHold);
        _drawStroke.Checked = _original.Gesture.DrawStroke;
        _strokeWidth.Value = Clamp(_original.Gesture.StrokeWidth, _strokeWidth);
        _validColorValue = ParseColor(_original.Gesture.ValidStrokeColor);
        _invalidColorValue = ParseColor(_original.Gesture.InvalidStrokeColor);
        _validColor.BackColor = _validColorValue;
        _invalidColor.BackColor = _invalidColorValue;
    }

    private void SetCombo(string key, string value) => SetScrollbarCombo(_modifierCombos[key], value);

    private static void SetScrollbarCombo(ComboBox combo, string value)
    {
        combo.Items.Clear();
        combo.Items.AddRange(ScrollBehaviorChoice.ChoicesIncluding(value));
        combo.SelectedItem = value;
        if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private bool ApplyChanges()
    {
        SaveProfileHeader();

        var scroll = _original.Scroll with
        {
            OnVerticalScrollbar = (string)_onVerticalScrollbar.SelectedItem!,
            OnHorizontalScrollbar = (string)_onHorizontalScrollbar.SelectedItem!,
            ModifierScroll = new ModifierScrollSettings
            {
                Shift = (string)_modifierCombos["Shift"].SelectedItem!,
                Ctrl = (string)_modifierCombos["Ctrl"].SelectedItem!,
                CtrlShift = (string)_modifierCombos["CtrlShift"].SelectedItem!,
                Alt = (string)_modifierCombos["Alt"].SelectedItem!,
                ShiftAlt = (string)_modifierCombos["ShiftAlt"].SelectedItem!,
                CtrlAlt = (string)_modifierCombos["CtrlAlt"].SelectedItem!,
            },
        };

        var gesture = _original.Gesture with
        {
            Range = (int)_range.Value,
            TimeoutMs = (int)_timeout.Value,
            PushHoldTimeMs = (int)_pushHold.Value,
            DrawStroke = _drawStroke.Checked,
            StrokeWidth = (int)_strokeWidth.Value,
            ValidStrokeColor = ToHex(_validColorValue),
            InvalidStrokeColor = ToHex(_invalidColorValue),
        };

        var tray = _original.Tray with
        {
            ShowTrayIcon = _showTrayIcon.Checked,
            ShowBalloonTip = _showBalloonTip.Checked,
        };

        var config = _original with
        {
            Gesture = gesture,
            Scroll = scroll,
            Tray = tray,
            Profiles = _profiles.Select(p => p.ToProfile()).ToArray(),
        };

        Applied?.Invoke(config);
        return true;
    }

    // ---------------------------------------------------------------- プロファイル操作
    private MutableProfile? Selected => _profileCombo.SelectedItem as MutableProfile;

    private void RefreshProfileCombo()
    {
        _profileCombo.Items.Clear();
        foreach (var p in _profiles)
            _profileCombo.Items.Add(p);
    }

    private void OnProfileSelected()
    {
        if (Selected is not { } p)
            return;
        _profilePattern.Text = p.ProcessPattern;
        _gesturesEnabled.Checked = p.GesturesEnabled;
        RefreshGestureList();
        RefreshWheelLabels();
    }

    private void SaveProfileHeader()
    {
        if (Selected is not { } p)
            return;
        p.ProcessPattern = string.IsNullOrWhiteSpace(_profilePattern.Text) ? "*" : _profilePattern.Text.Trim();
        p.GesturesEnabled = _gesturesEnabled.Checked;
        // コンボの表示更新
        int idx = _profileCombo.SelectedIndex;
        _profileCombo.Items[idx] = p;
    }

    private void AddProfile()
    {
        var p = new MutableProfile { Name = $"Profile{_profiles.Count + 1}", ProcessPattern = "" };
        _profiles.Add(p);
        RefreshProfileCombo();
        _profileCombo.SelectedItem = p;
    }

    private void RemoveProfile()
    {
        if (Selected is not { } p)
            return;
        if (_profiles.Count <= 1)
        {
            MessageBox.Show(this, "最後のプロファイルは削除できません。", "Clemoutis",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _profiles.Remove(p);
        RefreshProfileCombo();
        _profileCombo.SelectedIndex = 0;
    }

    // ---------------------------------------------------------------- ジェスチャー操作
    private void RefreshGestureList()
    {
        _gestureList.Items.Clear();
        if (Selected is not { } p)
            return;
        foreach (var g in p.Gestures)
        {
            var item = new ListViewItem(g.Strokes);
            item.SubItems.Add(ActionDisplay.Describe(g.Action));
            _gestureList.Items.Add(item);
        }
    }

    private void AddGesture()
    {
        if (Selected is not { } p)
            return;
        using var dlg = new GestureEditDialog(null);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result is { } binding)
        {
            p.Gestures.Add(binding);
            RefreshGestureList();
        }
    }

    private void EditGesture()
    {
        if (Selected is not { } p || _gestureList.SelectedIndices.Count == 0)
            return;
        int idx = _gestureList.SelectedIndices[0];
        using var dlg = new GestureEditDialog(p.Gestures[idx]);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result is { } binding)
        {
            p.Gestures[idx] = binding;
            RefreshGestureList();
        }
    }

    private void RemoveGesture()
    {
        if (Selected is not { } p || _gestureList.SelectedIndices.Count == 0)
            return;
        p.Gestures.RemoveAt(_gestureList.SelectedIndices[0]);
        RefreshGestureList();
    }

    // ---------------------------------------------------------------- 色
    private void PickColor(ref Color value, Button button)
    {
        using var dlg = new ColorDialog { Color = value };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            value = dlg.Color;
            button.BackColor = value;
        }
    }

    private static decimal Clamp(int v, NumericUpDown n) =>
        Math.Min(n.Maximum, Math.Max(n.Minimum, v));

    private static Color ParseColor(string hex)
    {
        try
        {
            string s = hex.TrimStart('#');
            if (s.Length == 6)
            {
                int r = int.Parse(s[..2], NumberStyles.HexNumber);
                int g = int.Parse(s[2..4], NumberStyles.HexNumber);
                int b = int.Parse(s[4..6], NumberStyles.HexNumber);
                return Color.FromArgb(r, g, b);
            }
        }
        catch (FormatException) { }
        return Color.Black;
    }

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
