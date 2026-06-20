namespace Clemotius.Core.Config;

/// <summary>トレイ表示の設定。既定値はユーザーの Kazaguru.ini 由来。</summary>
public sealed record TraySettings
{
    public int MenuStyle { get; init; } = 2;

    /// <summary>トレイアイコンのダブルクリック時の動作。0=設定を開く, 1=一時停止の切り替え。</summary>
    public int DoubleClickAction { get; init; }
}
