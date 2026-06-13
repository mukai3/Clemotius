using Clemotius.Interop;

namespace Clemotius.Hooks;

internal sealed class KeyboardHook : LowLevelHook
{
    public KeyboardHook() : base(NativeMethods.WH_KEYBOARD_LL) { }

    /// <summary>(message, data) を受け取り、飲み込むなら true を返す。</summary>
    public Func<int, NativeMethods.KBDLLHOOKSTRUCT, bool>? Handler { get; set; }

    protected override unsafe bool Handle(nint wParam, nint lParam)
    {
        ref readonly var data = ref *(NativeMethods.KBDLLHOOKSTRUCT*)lParam;
        if ((data.flags & NativeMethods.LLKHF_INJECTED) != 0)
            return false;
        return Handler?.Invoke((int)wParam, data) ?? false;
    }
}
