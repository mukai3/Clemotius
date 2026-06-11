using Clemoutis.Core.Config;

namespace Clemoutis.Core.Scroll;

/// <summary>
/// 修飾キーの押下状態から、適用すべきホイール変換を決める。Win32 非依存。
/// オリジナル v1.67 に合わせ、押下中の修飾キーの組み合わせに完全一致する
/// スロットを引く（Shift / Ctrl / Ctrl+Shift / Alt / Shift+Alt / Ctrl+Alt）。
/// 上記以外の組み合わせ（修飾なし、Ctrl+Shift+Alt、Win 等）は None。
/// </summary>
public sealed class ModifierScrollResolver
{
    private readonly ModifierScrollSettings _settings;

    public ModifierScrollResolver(ModifierScrollSettings settings)
    {
        _settings = settings;
    }

    public WheelConversion Resolve(IModifierState m)
    {
        // 押下中の Ctrl/Shift/Alt の組み合わせに完全一致するスロットだけを採用
        string? behavior = (m.Ctrl, m.Shift, m.Alt) switch
        {
            (false, true, false) => _settings.Shift,
            (true, false, false) => _settings.Ctrl,
            (true, true, false) => _settings.CtrlShift,
            (false, false, true) => _settings.Alt,
            (false, true, true) => _settings.ShiftAlt,
            (true, false, true) => _settings.CtrlAlt,
            _ => null,
        };
        return ScrollBehaviorParser.Parse(behavior);
    }
}
