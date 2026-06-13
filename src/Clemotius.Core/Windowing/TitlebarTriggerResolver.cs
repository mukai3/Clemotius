using Clemotius.Core.Config;

namespace Clemotius.Core.Windowing;

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
    /// <summary>
    /// ヒットテスト前の事前判定: このボタン＋修飾キーで成立し得るスロットが
    /// 1つでも設定されているか。false ならヒットテスト（クロスプロセスの
    /// WM_NCHITTEST 送信）自体を省略でき、クリックへの遅延を避けられる。
    /// </summary>
    public static bool MayMatch(TitlebarSettings settings, TitlebarButton button, bool shift, bool ctrl)
        => button switch
        {
            TitlebarButton.Left =>
                (shift && !ctrl && WindowActionParser.Parse(settings.ShiftClick) is not null)
                || (ctrl && !shift && WindowActionParser.Parse(settings.CtrlClick) is not null),
            TitlebarButton.Right =>
                WindowActionParser.Parse(settings.RightClick) is not null
                || WindowActionParser.Parse(settings.MinButtonRightClick) is not null
                || WindowActionParser.Parse(settings.CloseButtonRightClick) is not null,
            TitlebarButton.Middle => WindowActionParser.Parse(settings.MiddleClick) is not null,
            _ => false,
        };

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
