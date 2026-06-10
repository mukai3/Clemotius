namespace Clemoutis.Core.Config;

/// <summary>トレイ表示の設定。既定値はユーザーの Kazaguru.ini 由来。</summary>
public sealed record TraySettings
{
    public bool ShowTrayIcon { get; init; } = true;
    public bool ShowBalloonTip { get; init; }
    public int MenuStyle { get; init; } = 2;
}
