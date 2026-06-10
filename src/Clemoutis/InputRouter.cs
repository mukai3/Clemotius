using Clemoutis.Hooks;
using Clemoutis.Interop;

namespace Clemoutis;

/// <summary>
/// フックから受けたイベントを各機能へ振り分け、飲み込むか素通しかを最終判断する。
/// フェーズ1では修飾キー追跡のみ行い全イベントを素通しする。
/// フェーズ2で GestureEngine、フェーズ4で ScrollEnhancer をここに接続する。
/// </summary>
internal sealed class InputRouter
{
    private readonly ModifierStateTracker _modifiers;

    public InputRouter(ModifierStateTracker modifiers)
    {
        _modifiers = modifiers;
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouse(int message, NativeMethods.MSLLHOOKSTRUCT data)
    {
        return false; // 素通し
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnKeyboard(int message, NativeMethods.KBDLLHOOKSTRUCT data)
    {
        _modifiers.OnKey(message, data.vkCode);
        return false; // 素通し
    }
}
