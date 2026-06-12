using Clemoutis.Core.Config;

namespace Clemoutis.Core.Windowing;

/// <summary>マウスボタン（タイトルバーアクションの判定用）。</summary>
public enum TitlebarButton
{
    Left,
    Right,
    Middle,
}

/// <summary>カーソル位置の非クライアント領域の種類。</summary>
public enum TitlebarHitArea
{
    None,
    Caption,         // タイトルバー
    MinimizeButton,  // 最小化ボタン
    CloseButton,     // 閉じるボタン
}

/// <summary>
/// ボタン・修飾キー・ヒット領域の組み合わせから、設定されたウィンドウ操作を決める。
/// Win32 非依存のピュアロジック。
/// </summary>
public static class TitlebarTriggerResolver
{
    /// <summary>該当するアクション。割り当てなし・対象外の組み合わせは null。</summary>
    public static WindowAction? Resolve(
        TitlebarSettings settings, TitlebarButton button, bool shift, bool ctrl, TitlebarHitArea area)
    {
        string? slot = (area, button) switch
        {
            // タイトルバー: Shift+左 / Ctrl+左（同時押しは対象外）/ 右 / 中央
            (TitlebarHitArea.Caption, TitlebarButton.Left) when shift && !ctrl => settings.ShiftClick,
            (TitlebarHitArea.Caption, TitlebarButton.Left) when ctrl && !shift => settings.CtrlClick,
            (TitlebarHitArea.Caption, TitlebarButton.Right) => settings.RightClick,
            (TitlebarHitArea.Caption, TitlebarButton.Middle) => settings.MiddleClick,
            // ボタン上の右クリック
            (TitlebarHitArea.MinimizeButton, TitlebarButton.Right) => settings.MinButtonRightClick,
            (TitlebarHitArea.CloseButton, TitlebarButton.Right) => settings.CloseButtonRightClick,
            _ => null,
        };
        return WindowActionParser.Parse(slot);
    }
}
