namespace Clemotius.Core.Windowing;

/// <summary>
/// タイトルバーアクションで実行するウィンドウ操作。
/// オリジナルには20種以上あるが、v1 は使用頻度の高い4種に絞る（設定値は文字列で保持
/// するため、将来の追加は enum とパーサに足すだけでよい）。
/// </summary>
public enum WindowAction
{
    AlwaysOnTop,    // 常に最前面（トグル）
    WindowShade,    // ウィンドウシェード＝タイトルバーだけに巻き上げ（トグル）
    OpenExeFolder,  // 実行ファイルのフォルダを開く
    Translucent,    // 半透明化（トグル）
}

public static class WindowActionParser
{
    /// <summary>設定文字列を解釈する。"none"/未知は null（割り当てなし）。</summary>
    public static WindowAction? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "alwaysontop" => WindowAction.AlwaysOnTop,
        "windowshade" => WindowAction.WindowShade,
        "openexefolder" => WindowAction.OpenExeFolder,
        "translucent" => WindowAction.Translucent,
        _ => null,
    };

    public static string ToConfigValue(WindowAction action) => action switch
    {
        WindowAction.AlwaysOnTop => "alwaysOnTop",
        WindowAction.WindowShade => "windowShade",
        WindowAction.OpenExeFolder => "openExeFolder",
        WindowAction.Translucent => "translucent",
        _ => "none",
    };
}
