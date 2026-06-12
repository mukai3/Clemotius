using Clemoutis.Core;
using Clemoutis.Core.Config;
using Clemoutis.Core.Windowing;
using Clemoutis.Interop;

namespace Clemoutis.Windowing;

/// <summary>
/// タイトルバーアクションの入力検出。ボタン押下時にカーソル下ウィンドウへ
/// WM_NCHITTEST を問い合わせ、設定されたトリガーに一致すれば押下を飲み込んで
/// ウィンドウ操作を実行する（対応する解放も1回飲み込む）。
/// </summary>
internal sealed class TitlebarActionHandler
{
    private readonly IModifierState _modifiers;
    private readonly WindowActionExecutor _executor;
    private volatile TitlebarSettings _settings;

    // 押下を飲み込んだボタンの解放を1回飲み込むためのフラグ
    private bool _swallowLeftUp;
    private bool _swallowRightUp;
    private bool _swallowMiddleUp;

    public TitlebarActionHandler(
        IModifierState modifiers, WindowActionExecutor executor, TitlebarSettings settings)
    {
        _modifiers = modifiers;
        _executor = executor;
        _settings = settings;
    }

    public void UpdateSettings(TitlebarSettings settings)
    {
        _settings = settings;
        _executor.OpacityPercent = settings.WindowOpacity;
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouse(int message, NativeMethods.MSLLHOOKSTRUCT data)
    {
        switch (message)
        {
            case NativeMethods.WM_LBUTTONDOWN:
                return OnButtonDown(TitlebarButton.Left, ref _swallowLeftUp, data);
            case NativeMethods.WM_RBUTTONDOWN:
                return OnButtonDown(TitlebarButton.Right, ref _swallowRightUp, data);
            case NativeMethods.WM_MBUTTONDOWN:
                return OnButtonDown(TitlebarButton.Middle, ref _swallowMiddleUp, data);

            case NativeMethods.WM_LBUTTONUP:
                return ConsumeSwallow(ref _swallowLeftUp);
            case NativeMethods.WM_RBUTTONUP:
                return ConsumeSwallow(ref _swallowRightUp);
            case NativeMethods.WM_MBUTTONUP:
                return ConsumeSwallow(ref _swallowMiddleUp);

            default:
                return false;
        }
    }

    private bool OnButtonDown(
        TitlebarButton button, ref bool swallowUp, NativeMethods.MSLLHOOKSTRUCT data)
    {
        // 全スロット none なら何もしない（ヒットテストのコスト回避）
        var s = _settings;
        if (s.ShiftClick == "none" && s.CtrlClick == "none" && s.RightClick == "none"
            && s.MiddleClick == "none" && s.MinButtonRightClick == "none"
            && s.CloseButtonRightClick == "none")
        {
            return false;
        }

        var area = HitTest(data.pt.X, data.pt.Y, out nint hwnd);
        if (area == TitlebarHitArea.None)
            return false;

        var action = TitlebarTriggerResolver.Resolve(
            s, button, _modifiers.Shift, _modifiers.Ctrl, area);
        if (action is null)
            return false;

        swallowUp = true;
        nint root = InputNative.GetAncestor(hwnd, InputNative.GA_ROOT);
        nint target = root != 0 ? root : hwnd;
        Task.Run(() => _executor.Execute(action.Value, target));
        return true; // 押下を飲み込む
    }

    private static bool ConsumeSwallow(ref bool flag)
    {
        if (!flag)
            return false;
        flag = false;
        return true;
    }

    private static TitlebarHitArea HitTest(int x, int y, out nint hwnd)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd == 0)
            return TitlebarHitArea.None;

        nint lParam = unchecked((nint)((y << 16) | (x & 0xFFFF)));
        nint hit = InputNative.SendMessageW(hwnd, InputNative.WM_NCHITTEST, 0, lParam);
        return (int)hit switch
        {
            InputNative.HTCAPTION => TitlebarHitArea.Caption,
            InputNative.HTMINBUTTON => TitlebarHitArea.MinimizeButton,
            InputNative.HTCLOSE => TitlebarHitArea.CloseButton,
            _ => TitlebarHitArea.None,
        };
    }
}
