namespace Clemoutis.Core.Config;

/// <summary>スクロール拡張の設定。既定値はユーザーの Kazaguru.ini 由来。</summary>
public sealed record ScrollSettings
{
    public int Sensitivity { get; init; } = 3;
    public int Acceleration { get; init; } = 3;
    public bool AcceleratedScroll { get; init; }
    public bool ScrollAlways { get; init; }
    public bool HorizontalOnScrollbar { get; init; } = true;
    public int MergeWheelDelta { get; init; } = 2;
    public int WheelResolution { get; init; } = 1;
    public int AutoWheelResolution { get; init; } = 3;
    public IReadOnlyList<ModifierScrollRule> ModifierRules { get; init; } =
        new[] { new ModifierScrollRule("Alt", "code:55") };
}

/// <summary>修飾キー押下中のホイール変換ルール。behavior の意味は動的解析で確定予定。</summary>
public sealed record ModifierScrollRule(string Modifier, string Behavior);
