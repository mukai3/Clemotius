using Clemoutis.Core.Actions;
using Clemoutis.Core.Gestures;

namespace Clemoutis.Core.Config;

/// <summary>
/// アプリ全体の設定ルート。既定値はユーザーの Kazaguru.ini からデコードした
/// ジェスチャー割り当てと設定値で構成する（使用感の踏襲）。
/// </summary>
public sealed record ClemoutisConfig
{
    public GestureSettings Gesture { get; init; } = new();
    public ScrollSettings Scroll { get; init; } = new();
    public TraySettings Tray { get; init; } = new();
    public IReadOnlyList<GestureProfile> Profiles { get; init; } = new[] { DefaultProfile() };

    /// <summary>ユーザー ini 由来の既定ジェスチャーを持つ Default プロファイル。</summary>
    public static GestureProfile DefaultProfile() => new()
    {
        Name = "Default",
        ProcessPattern = "*",
        GesturesEnabled = true,
        Gestures = new[]
        {
            new GestureBinding("L", new AppCommandAction(AppCommand.BrowserBackward)),
            new GestureBinding("R", new AppCommandAction(AppCommand.BrowserForward)),
            new GestureBinding("DR", new KeyAction(KeyStrokeParser.Parse("Ctrl+W"))),
            new GestureBinding("UDU", new KeyAction(KeyStrokeParser.Parse("Ctrl+F5"))),
            new GestureBinding("DUD", new KeyAction(KeyStrokeParser.Parse("Ctrl+F5"))),
            new GestureBinding("LU", new KeyAction(KeyStrokeParser.Parse("Ctrl+Home"))),
            new GestureBinding("RU", new KeyAction(KeyStrokeParser.Parse("Ctrl+Home"))),
            new GestureBinding("LD", new KeyAction(KeyStrokeParser.Parse("Ctrl+End"))),
            new GestureBinding("RD", new KeyAction(KeyStrokeParser.Parse("Ctrl+End"))),
        },
        // ユーザー ini の R+WU / R+WD（右ボタン+ホイール）由来
        WheelUp = new KeyAction(KeyStrokeParser.Parse("Ctrl+Shift+Tab")),   // 前のタブ
        WheelDown = new KeyAction(KeyStrokeParser.Parse("Ctrl+Tab")),       // 次のタブ
    };

    public static ClemoutisConfig CreateDefault() => new();
}
