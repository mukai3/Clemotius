namespace Clemoutis.SettingsUi;

/// <summary>タイトルバーアクションの選択肢カタログ（値は WindowActionParser の設定文字列）。</summary>
internal static class TitlebarActionChoice
{
    public static IReadOnlyList<ScrollBehaviorChoice.Choice> All { get; } = new[]
    {
        new ScrollBehaviorChoice.Choice("なし", "none"),
        new ScrollBehaviorChoice.Choice("常に最前面", "alwaysOnTop"),
        new ScrollBehaviorChoice.Choice("ウィンドウシェード", "windowShade"),
        new ScrollBehaviorChoice.Choice("実行ファイルのフォルダを開く", "openExeFolder"),
        new ScrollBehaviorChoice.Choice("半透明化", "translucent"),
    };
}
