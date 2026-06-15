using Clemotius.Interop;

namespace Clemotius.Hooks;

internal sealed class MouseHook : LowLevelHook
{
    public MouseHook() : base(NativeMethods.WH_MOUSE_LL) { }

    /// <summary>(message, data) を受け取り、飲み込むなら true を返す。</summary>
    public Func<int, NativeMethods.MSLLHOOKSTRUCT, bool>? Handler { get; set; }

    protected override unsafe bool Handle(nint wParam, nint lParam)
    {
        ref readonly var data = ref *(NativeMethods.MSLLHOOKSTRUCT*)lParam;
        // 自分が注入したイベントは判定対象にしない（無限ループ防止／生存プローブ）
        if ((data.flags & NativeMethods.LLMHF_INJECTED) != 0)
            return false;
        NoteRealEvent();
        return Handler?.Invoke((int)wParam, data) ?? false;
    }
}
