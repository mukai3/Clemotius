using Clemotius.Core;
using Clemotius.Core.Config;
using Clemotius.Core.Scroll;
using Clemotius.Interop;
using ScrollOrientation = Clemotius.Core.Scroll.ScrollOrientation;

namespace Clemotius.Scroll;

/// <summary>
/// ホイールイベントの拡張処理。オリジナル同様、対象ウィンドウへ WM_HSCROLL/WM_VSCROLL を
/// 直接送ってスクロールバーを操作する（MOUSEEVENTF_HWHEEL では多くのウィンドウで効かないため）。
///   1. 修飾キー押下中の挙動（scroll.modifierScroll）
///   2. スクロールバー上での縦ホイール → 設定挙動（垂直/水平バーで別々）
/// どちらにも該当しなければ素通しする。
/// </summary>
internal sealed class ScrollEnhancer
{
    private readonly IModifierState _modifiers;
    private volatile ScrollSettings _settings;
    private volatile ModifierScrollResolver _resolver;

    public ScrollEnhancer(IModifierState modifiers, ScrollSettings settings)
    {
        _modifiers = modifiers;
        _settings = settings;
        _resolver = new ModifierScrollResolver(settings.ModifierScroll);
    }

    public void UpdateSettings(ScrollSettings settings)
    {
        _settings = settings;
        _resolver = new ModifierScrollResolver(settings.ModifierScroll);
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouseWheel(NativeMethods.MSLLHOOKSTRUCT data)
    {
        // mouseData 上位ワードが符号付きホイールデルタ（正=上/奥）
        short delta = (short)(data.mouseData >> 16);
        if (delta == 0)
            return false;

        ScrollAction? action;
        nint target;

        // 1) 修飾キー押下中の挙動が優先（向きはコード値域で判定）
        string? modBehavior = _resolver.ResolveBehavior(_modifiers);
        if (modBehavior is not null and not "none")
        {
            action = ScrollCodeDecoder.Decode(modBehavior, slot: null);
            target = WindowUnderCursor(data.pt);
        }
        else
        {
            // 2) スクロールバー上判定（向きごとの設定挙動）
            var (hit, hwnd) = ScrollBarDetector.Detect(data.pt.X, data.pt.Y);
            (action, target) = hit switch
            {
                ScrollBarHit.Horizontal =>
                    (ScrollCodeDecoder.Decode(_settings.OnHorizontalScrollbar, ScrollOrientation.Horizontal), hwnd),
                ScrollBarHit.Vertical =>
                    (ScrollCodeDecoder.Decode(_settings.OnVerticalScrollbar, ScrollOrientation.Vertical), hwnd),
                _ => (null, (nint)0),
            };
        }

        if (action is null || target == 0)
            return false; // 素通し

        SendScroll(target, action, delta);
        return true; // 元のホイールは飲み込む
    }

    private static nint WindowUnderCursor(NativeMethods.POINT pt)
        => InputNative.WindowFromPoint(pt);

    private static void SendScroll(nint target, ScrollAction action, short delta)
    {
        // ホイール上(delta>0)=先頭方向(back)、下(delta<0)=末尾方向(forward)
        bool forward = delta < 0;
        uint msg = action.Orientation == ScrollOrientation.Horizontal
            ? InputNative.WM_HSCROLL
            : InputNative.WM_VSCROLL;

        switch (action.Unit)
        {
            case ScrollUnit.Line:
                int code = forward ? InputNative.SB_LINEFWD : InputNative.SB_LINEBACK;
                for (int i = 0; i < Math.Max(1, action.Amount); i++)
                    InputNative.PostMessageW(target, msg, code, 0);
                InputNative.PostMessageW(target, msg, InputNative.SB_ENDSCROLL, 0);
                break;
            case ScrollUnit.Page:
                int pcode = forward ? InputNative.SB_PAGEFWD : InputNative.SB_PAGEBACK;
                InputNative.PostMessageW(target, msg, pcode, 0);
                InputNative.PostMessageW(target, msg, InputNative.SB_ENDSCROLL, 0);
                break;
            case ScrollUnit.Edge:
                int ecode = forward ? InputNative.SB_END : InputNative.SB_HOME;
                InputNative.PostMessageW(target, msg, ecode, 0);
                InputNative.PostMessageW(target, msg, InputNative.SB_ENDSCROLL, 0);
                break;
        }
    }
}
