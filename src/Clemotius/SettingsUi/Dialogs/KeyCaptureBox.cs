using System.Windows.Input;
using Clemotius.Core.Actions;
using Clemotius.Interop;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>
/// 実際のキー押下をキャプチャして <see cref="KeyStroke"/> を設定する WPF 版テキストボックス。
/// フォーカス中にキーを押すと「Ctrl+Shift+Tab」のように表示・保持する。
/// </summary>
internal sealed class KeyCaptureBox : System.Windows.Controls.TextBox
{
    private const string Placeholder = "(クリックしてキーを押す)";
    private KeyStroke? _stroke;

    public KeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = System.Windows.Input.Cursors.Hand;
        Text = Placeholder;
    }

    /// <summary>現在保持しているキー。未設定なら null。</summary>
    public KeyStroke? Stroke
    {
        get => _stroke;
        set
        {
            _stroke = value;
            Text = value?.ToString() ?? Placeholder;
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (_stroke is null)
            Text = "キーを押してください…";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Text = _stroke?.ToString() ?? Placeholder;
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true; // Tab/矢印含めすべてキャプチャ対象にする

        // Alt 併用時は実キーが SystemKey 側に入る
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

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
