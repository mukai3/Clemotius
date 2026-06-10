using Clemoutis.Core;
using Clemoutis.Interop;

namespace Clemoutis.Hooks;

/// <summary>
/// KeyboardHook のイベントから修飾キーの押下状態を追跡する。
/// volatile bool のみで構成しフックコールバックから割り当てなしで更新できる。
/// </summary>
internal sealed class ModifierStateTracker : IModifierState
{
    private volatile bool _shift;
    private volatile bool _ctrl;
    private volatile bool _alt;
    private volatile bool _win;

    public bool Shift => _shift;
    public bool Ctrl => _ctrl;
    public bool Alt => _alt;
    public bool Win => _win;

    public void OnKey(int message, uint vkCode)
    {
        bool down = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        switch (vkCode)
        {
            case NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT:
                _shift = down;
                break;
            case NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL:
                _ctrl = down;
                break;
            case NativeMethods.VK_LMENU or NativeMethods.VK_RMENU:
                _alt = down;
                break;
            case NativeMethods.VK_LWIN or NativeMethods.VK_RWIN:
                _win = down;
                break;
        }
    }

    /// <summary>フック再設置時など、取りこぼしの可能性があるときに状態を初期化する。</summary>
    public void Reset()
    {
        _shift = _ctrl = _alt = _win = false;
    }
}
