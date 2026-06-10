using Clemoutis.Gestures;
using Clemoutis.Hooks;
using Clemoutis.Interop;

namespace Clemoutis;

/// <summary>
/// フックから受けたイベントを各機能へ振り分け、飲み込むか素通しかを最終判断する。
/// フェーズ2では GestureEngine を接続。フェーズ4で ScrollEnhancer を追加する。
/// </summary>
internal sealed class InputRouter
{
    private readonly ModifierStateTracker _modifiers;
    private readonly GestureEngine _gesture;

    public InputRouter(ModifierStateTracker modifiers, GestureEngine gesture)
    {
        _modifiers = modifiers;
        _gesture = gesture;
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouse(int message, NativeMethods.MSLLHOOKSTRUCT data)
    {
        return _gesture.OnMouse(message, data);
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnKeyboard(int message, NativeMethods.KBDLLHOOKSTRUCT data)
    {
        _modifiers.OnKey(message, data.vkCode);
        return false; // 素通し
    }
}
