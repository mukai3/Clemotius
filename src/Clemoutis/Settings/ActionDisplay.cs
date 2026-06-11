using Clemoutis.Core.Actions;

namespace Clemoutis.Settings;

/// <summary>設定画面でのアクション表示・編集用のヘルパー。</summary>
internal static class ActionDisplay
{
    public const string TypeKey = "キー送信";
    public const string TypePreset = "プリセット";
    public const string TypeAppCommand = "コマンド";
    public const string TypeClose = "閉じる";

    // アクション種別「閉じる」(WM_CLOSE) は選択肢から除外（コマンドの Close で代替可能）。
    // CloseAction の表示は既存データ互換のため Describe/TypeNameOf には残す。
    // プリセットは入力補助で、保存時はキー送信(KeyAction)に展開される。
    public static string[] TypeNames => new[] { TypeKey, TypePreset, TypeAppCommand };

    /// <summary>一覧表示用の説明文。</summary>
    public static string Describe(GestureAction action) => action switch
    {
        KeyAction k => $"{TypeKey}: {k.Stroke}",
        AppCommandAction c => $"{TypeAppCommand}: {c.Command}",
        CloseAction => TypeClose,
        _ => "(不明)",
    };

    public static string TypeNameOf(GestureAction action) => action switch
    {
        KeyAction => TypeKey,
        AppCommandAction => TypeAppCommand,
        CloseAction => TypeClose,
        _ => TypeKey,
    };

    /// <summary>画面上のコマンド表示用の簡潔なラベル（例: "Ctrl+W" / "Close"）。</summary>
    public static string ShortLabel(GestureAction action) => action switch
    {
        KeyAction k => k.Stroke.ToString(),
        AppCommandAction c => c.Command.ToString(),
        CloseAction => "Close",
        _ => "",
    };
}
