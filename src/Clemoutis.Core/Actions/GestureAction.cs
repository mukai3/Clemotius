namespace Clemoutis.Core.Actions;

/// <summary>
/// ジェスチャーに割り当てるアクション。設計の3機構（キー送信 / WM_APPCOMMAND / WM_CLOSE）を
/// 判別共用体として表現する。実行（Win32 副作用）は App 側の ActionExecutor が担う。
/// </summary>
public abstract record GestureAction;

/// <summary>キーストローク送信。例: Ctrl+W。</summary>
public sealed record KeyAction(KeyStroke Stroke) : GestureAction;

/// <summary>WM_APPCOMMAND 送信（ブラウザの戻る/進む/更新など）。</summary>
public sealed record AppCommandAction(AppCommand Command) : GestureAction;

/// <summary>WM_CLOSE 送信（ウィンドウを閉じる）。</summary>
public sealed record CloseAction : GestureAction;
