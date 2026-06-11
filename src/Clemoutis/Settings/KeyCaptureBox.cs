using Clemoutis.Core.Actions;
using Clemoutis.Interop;

namespace Clemoutis.Settings;

/// <summary>
/// 実際のキー押下をキャプチャして <see cref="KeyStroke"/> を設定する読み取り専用テキストボックス。
/// フォーカス中にキーを押すと「Ctrl+Shift+Tab」のように表示・保持する。
/// </summary>
internal sealed class KeyCaptureBox : TextBox
{
    private KeyStroke? _stroke;

    public KeyCaptureBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Cursor = Cursors.Hand;
        Text = "(クリックしてキーを押す)";
    }

    /// <summary>現在保持しているキー。未設定なら null。</summary>
    public KeyStroke? Stroke
    {
        get => _stroke;
        set
        {
            _stroke = value;
            Text = value?.ToString() ?? "(クリックしてキーを押す)";
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        if (_stroke is null)
            Text = "キーを押してください…";
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Text = _stroke?.ToString() ?? "(クリックしてキーを押す)";
    }

    // 矢印・Tab 等もキャプチャ対象にする
    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        var key = e.KeyCode;
        // 修飾キー単独の押下は主キーとして採用しない
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu
            or Keys.LControlKey or Keys.RControlKey
            or Keys.LShiftKey or Keys.RShiftKey
            or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        // Win キーは Control.ModifierKeys に反映されないため実押下状態を直接確認する
        bool win = NativeMethods.IsKeyDown(NativeMethods.VK_LWIN)
                || NativeMethods.IsKeyDown(NativeMethods.VK_RWIN);

        _stroke = new KeyStroke(
            (ushort)key,
            Ctrl: e.Control,
            Shift: e.Shift,
            Alt: e.Alt,
            Win: win);
        Text = _stroke.Value.ToString();
    }
}
