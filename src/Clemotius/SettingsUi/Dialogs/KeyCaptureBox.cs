using System.Windows.Input;
using Clemotius.Core.Actions;
using Clemotius.Interop;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>
/// 実際のキー押下をキャプチャして <see cref="KeyStroke"/> を設定する WPF 版テキストボックス。
/// フォーカス中にキーを押すと「Ctrl+Shift+Tab」のように表示・保持する。
/// 見た目を他の入力欄に合わせるため Wpf.Ui の TextBox を継承し、未設定時はプレースホルダ表示。
/// </summary>
internal sealed class KeyCaptureBox : Wpf.Ui.Controls.TextBox
{
    private const string Placeholder = "(クリックしてキーを押す)";
    private const string FocusedHint = "キーを押してください…";
    private KeyStroke? _stroke;

    public KeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        ClearButtonEnabled = false; // 読み取り専用なのでクリアボタンは出さない
        Cursor = System.Windows.Input.Cursors.Hand;
        PlaceholderText = Placeholder;
    }

    /// <summary>現在保持しているキー。未設定なら null。</summary>
    public KeyStroke? Stroke
    {
        get => _stroke;
        set
        {
            _stroke = value;
            Text = value?.ToString() ?? "";
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (_stroke is null)
            PlaceholderText = FocusedHint;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        PlaceholderText = Placeholder;
        Text = _stroke?.ToString() ?? "";
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        // Alt 併用時は実キーが SystemKey 側に入る
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        // 修飾なしの Tab / Shift+Tab はキャプチャせずフォーカス移動に使う(欄から抜けられるように)。
        // Esc もキャプチャせずダイアログのキャンセル(IsCancel)へ通す。これらは Handled にしない。
        // Ctrl+Tab 等、修飾キー併用の組み合わせは従来どおりキャプチャ対象にする。
        bool bareTab = key == Key.Tab && (modifiers & ~ModifierKeys.Shift) == ModifierKeys.None;
        if (bareTab || key == Key.Escape)
            return;

        e.Handled = true; // 上記以外はすべてキャプチャ対象にする

        // 修飾キー単独の押下は主キーとして採用しない
        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            return;
        }

        var mods = Keyboard.Modifiers;
        // Win キーは Modifiers に反映されないため実押下状態を直接確認する
        bool win = NativeMethods.IsKeyDown(NativeMethods.VK_LWIN)
                || NativeMethods.IsKeyDown(NativeMethods.VK_RWIN);

        _stroke = new KeyStroke(
            (ushort)KeyInterop.VirtualKeyFromKey(key),
            Ctrl: mods.HasFlag(ModifierKeys.Control),
            Shift: mods.HasFlag(ModifierKeys.Shift),
            Alt: mods.HasFlag(ModifierKeys.Alt),
            Win: win);
        Text = _stroke.Value.ToString();
    }
}
