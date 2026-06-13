namespace Clemotius.Core.Actions;

/// <summary>
/// ジェスチャーに割り当てるアクション。設計の3機構（キー送信 / WM_APPCOMMAND / WM_CLOSE）を
/// 判別共用体として表現する。実行（Win32 副作用）は App 側の ActionExecutor が担う。
/// </summary>
public abstract record GestureAction;

/// <summary>
/// キーストローク送信。例: Ctrl+W。
/// <paramref name="Label"/> はプリセット由来の表示名（例 "Chrome: 新規タブを開く"）。
/// null なら通常のキー送信としてキー名を表示する。
/// </summary>
public sealed record KeyAction(KeyStroke Stroke, string? Label = null) : GestureAction;

/// <summary>WM_APPCOMMAND 送信（ブラウザの戻る/進む/更新など）。</summary>
public sealed record AppCommandAction(AppCommand Command) : GestureAction;

/// <summary>WM_CLOSE 送信（ウィンドウを閉じる）。</summary>
public sealed record CloseAction : GestureAction;
