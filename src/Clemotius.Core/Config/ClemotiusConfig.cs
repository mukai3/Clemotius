using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.Core.Config;

/// <summary>
/// アプリ全体の設定ルート。既定値はユーザーの Kazaguru.ini からデコードした
/// ジェスチャー割り当てと設定値で構成する（使用感の踏襲）。
/// </summary>
public sealed record ClemotiusConfig
{
    public GestureSettings Gesture { get; init; } = new();
    public ScrollSettings Scroll { get; init; } = new();
    public TitlebarSettings Titlebar { get; init; } = new();
    public TraySettings Tray { get; init; } = new();
    public IReadOnlyList<GestureProfile> Profiles { get; init; } = new[] { DefaultBrowserProfile() };

    /// <summary>
    /// 既定の Web ブラウザ用プロファイル（chrome / msedge）。戻る/進む・タブ閉じ・再読込・
    /// 先頭/末尾移動・右ボタン+ホイールでのタブ切替を持つ。グローバル既定は廃止したため、
    /// 戻る/進む（L/R）もこのプロファイルに含める。
    /// </summary>
    public static GestureProfile DefaultBrowserProfile() => new()
    {
        Name = "ブラウザ",
        ProcessPattern = "chrome, msedge",
        GesturesEnabled = true,
        Gestures = new[]
        {
            new GestureBinding("L", new AppCommandAction(AppCommand.BrowserBackward)),  // ← 戻る
            new GestureBinding("R", new AppCommandAction(AppCommand.BrowserForward)),   // → 進む
            new GestureBinding("RU", new KeyAction(KeyStrokeParser.Parse("Ctrl+Home"))), // →↑ 先頭へ
            new GestureBinding("RD", new KeyAction(KeyStrokeParser.Parse("Ctrl+End"))),  // →↓ 末尾へ
            new GestureBinding("DR", new AppCommandAction(AppCommand.Close)),            // ↓→ タブを閉じる(APPCOMMAND_CLOSE。CloseAction=WM_CLOSEだとウィンドウごと閉じる)
            new GestureBinding("LR", new KeyAction(KeyStrokeParser.Parse("Ctrl+T"), "Chrome: 新規タブを開く (Ctrl+T)")),       // ←→
            new GestureBinding("DL", new KeyAction(KeyStrokeParser.Parse("Ctrl+Shift+T"), "Chrome: 最近閉じたタブを開く (Ctrl+Shift+T)")), // ↓←
            new GestureBinding("UDUD", new KeyAction(KeyStrokeParser.Parse("Ctrl+F5"), "Chrome: 完全再読み込み (Ctrl+F5)")),    // ↑↓↑↓
            new GestureBinding("UD", new AppCommandAction(AppCommand.BrowserRefresh)),   // ↑↓ 再読み込み
        },
        // ユーザー ini の R+WU / R+WD（右ボタン+ホイール）由来
        WheelUp = new KeyAction(KeyStrokeParser.Parse("Ctrl+Shift+Tab")),   // 前のタブ
        WheelDown = new KeyAction(KeyStrokeParser.Parse("Ctrl+Tab")),       // 次のタブ
    };

    /// <summary>
    /// per-app プロファイルのみモデルへの移行: 旧グローバル("*")プロファイルを取り除く。
    /// マージ廃止により "*" は意味を持たない（どのプロセス名にも一致しない）ため、読込時に
    /// 除去する。空になっても補充はしない（一致プロファイルが無ければジェスチャーは無効）。
    /// </summary>
    public ClemotiusConfig WithoutGlobalProfiles() => this with
    {
        Profiles = Profiles.Where(p => p.ProcessPattern.Trim() != "*").ToArray(),
    };

    public static ClemotiusConfig CreateDefault() => new();
}
