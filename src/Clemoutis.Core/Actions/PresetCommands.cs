namespace Clemoutis.Core.Actions;

/// <summary>
/// よく使うショートカットのプリセット。選択するとキー送信（<see cref="KeyAction"/>）に
/// 展開される。アプリ別カテゴリで分類し、随時追加していく。
/// </summary>
public sealed record PresetCommand(string Category, string Name, KeyStroke Stroke)
{
    /// <summary>コンボ表示用（例 "Chrome: 新規タブを開く (Ctrl+T)"）。</summary>
    public string Display => $"{Category}: {Name}";
}

public static class PresetCommands
{
    public static IReadOnlyList<PresetCommand> All { get; } = new[]
    {
        new PresetCommand("Chrome", "新規タブを開く (Ctrl+T)", KeyStrokeParser.Parse("Ctrl+T")),
        new PresetCommand("Chrome", "最近閉じたタブを開く (Ctrl+Shift+T)", KeyStrokeParser.Parse("Ctrl+Shift+T")),
        new PresetCommand("Chrome", "完全再読み込み (Ctrl+F5)", KeyStrokeParser.Parse("Ctrl+F5")),
        new PresetCommand("Chrome", "DevTools を開く (F12)", KeyStrokeParser.Parse("F12")),
    };
}
