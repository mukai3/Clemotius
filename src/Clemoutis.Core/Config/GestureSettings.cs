namespace Clemoutis.Core.Config;

/// <summary>
/// ジェスチャー認識・描画の設定。既定値はユーザーの Kazaguru.ini 由来。
/// </summary>
public sealed record GestureSettings
{
    public int Range { get; init; } = 8;
    public int TimeoutMs { get; init; } = 1000;
    public int PushHoldTimeMs { get; init; } = 500;
    public bool RapidFire { get; init; }
    public bool DrawStroke { get; init; }
    public int DrawingType { get; init; }
    public int StrokeWidth { get; init; } = 2;
    public string ValidStrokeColor { get; init; } = "#80FF00";
    public string InvalidStrokeColor { get; init; } = "#FFFF00";
}
