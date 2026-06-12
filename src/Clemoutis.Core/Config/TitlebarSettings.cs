namespace Clemoutis.Core.Config;

/// <summary>
/// タイトルバーアクションの設定。オリジナル「ウィンドウ」タブのタイトルバーアクションに対応。
/// 各スロットの値は "none" / "alwaysOnTop" / "windowShade" / "openExeFolder" / "translucent"。
/// </summary>
public sealed record TitlebarSettings
{
    public string ShiftClick { get; init; } = "none";
    public string CtrlClick { get; init; } = "none";
    public string RightClick { get; init; } = "none";
    public string MiddleClick { get; init; } = "none";
    public string MinButtonRightClick { get; init; } = "none";
    public string CloseButtonRightClick { get; init; } = "none";

    /// <summary>半透明化の不透明度（%）。ユーザー ini の WindowOpacity 由来。</summary>
    public int WindowOpacity { get; init; } = 50;
}
