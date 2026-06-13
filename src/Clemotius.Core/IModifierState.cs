namespace Clemotius.Core;

/// <summary>
/// 修飾キーの現在の押下状態。KeyboardHook が更新し、
/// ジェスチャー/スクロール拡張が参照する。
/// </summary>
public interface IModifierState
{
    bool Shift { get; }
    bool Ctrl { get; }
    bool Alt { get; }
    bool Win { get; }
}
