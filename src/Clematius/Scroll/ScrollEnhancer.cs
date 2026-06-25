using Clematius.Core;
using Clematius.Core.Config;
using Clematius.Core.Scroll;
using Clematius.Interop;
using ScrollOrientation = Clematius.Core.Scroll.ScrollOrientation;

namespace Clematius.Scroll;

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
    private int _diagInFlight; // 診断ログの多重起動防止 (0/1)

    public ScrollEnhancer(IModifierState modifiers, ScrollSettings settings)
    {
        _modifiers = modifiers;
        _settings = settings;
        _resolver = new ModifierScrollResolver(settings.ModifierScroll);
        ScrollBarDetector.Configure(settings.DetectOfficeScrollbar, settings.DetectBrowserScrollbar);
    }

    public void UpdateSettings(ScrollSettings settings)
    {
        _settings = settings;
        _resolver = new ModifierScrollResolver(settings.ModifierScroll);
        ScrollBarDetector.Configure(settings.DetectOfficeScrollbar, settings.DetectBrowserScrollbar);
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouseWheel(NativeMethods.MSLLHOOKSTRUCT data)
    {
        // mouseData 上位ワードが符号付きホイールデルタ（正=上/奥）
        short delta = (short)(data.mouseData >> 16);
        if (delta == 0)
            return false;

        // 切り分け用ログ（CLEMATIUS_SCROLL_DIAG=1 のときのみ）。重い検出はバックグラウンドで実行する。
        if (ScrollDiagnostics.Enabled && Interlocked.CompareExchange(ref _diagInFlight, 1, 0) == 0)
        {
            int dx = data.pt.X, dy = data.pt.Y;
            Task.Run(() =>
            {
                try { ScrollDiagnostics.Log(ScrollBarDetector.Describe(dx, dy)); }
                finally { Volatile.Write(ref _diagInFlight, 0); }
            });
        }

        ScrollAction? action;
        nint target;
        bool wheel = false; // true: 対象が WM_VSCROLL 不可のカスタムバー → WM_MOUSEWHEEL で送る

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
            var (hit, hwnd, useWheel) = ScrollBarDetector.Detect(data.pt.X, data.pt.Y);
            wheel = useWheel;
            action = hit switch
            {
                ScrollBarHit.Horizontal =>
                    ScrollCodeDecoder.Decode(_settings.OnHorizontalScrollbar, ScrollOrientation.Horizontal),
                ScrollBarHit.Vertical =>
                    ScrollCodeDecoder.Decode(_settings.OnVerticalScrollbar, ScrollOrientation.Vertical),
                _ => null,
            };
            target = hit == ScrollBarHit.None ? 0 : hwnd;
        }

        if (action is null || target == 0)
            return false; // 素通し

        SendScroll(target, action, delta, data.pt.X, data.pt.Y, wheel);
        return true; // 元のホイールは飲み込む
    }

    private static nint WindowUnderCursor(NativeMethods.POINT pt)
        => InputNative.WindowFromPoint(pt);

    private static void SendScroll(nint target, ScrollAction action, short delta, int x, int y, bool wheel)
    {
        // ホイール上(delta>0)=先頭方向(back)、下(delta<0)=末尾方向(forward)
        bool forward = delta < 0;

        // WM_VSCROLL を受け付けないカスタムバー（Excel 等）は WM_MOUSEWHEEL/WM_MOUSEHWHEEL で送る。
        // 量はノッチ数で近似（ページ/端は固定ノッチで近似する）。
        if (wheel)
        {
            SendWheel(target, action, forward, x, y);
            return;
        }

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

    /// <summary>
    /// WM_MOUSEWHEEL / WM_MOUSEHWHEEL でスクロールする（Excel 等 WM_VSCROLL 不可の相手向け）。
    /// ページ/端は正確に再現できないためノッチ数で近似する。
    /// </summary>
    private static void SendWheel(nint target, ScrollAction action, bool forward, int x, int y)
    {
        int notches = action.Unit switch
        {
            ScrollUnit.Line => Math.Max(1, action.Amount),
            ScrollUnit.Page => 6,   // 1ページ≒6ノッチで近似
            ScrollUnit.Edge => 60,  // 端まで＝大量ノッチで近似
            _ => 1,
        };
        int amount = notches * InputNative.WHEEL_DELTA;
        nint lParam = unchecked((nint)((y << 16) | (x & 0xFFFF)));

        if (action.Orientation == ScrollOrientation.Horizontal)
        {
            // WM_MOUSEHWHEEL: 正=右。forward(右/末尾)=正
            InputNative.PostMessageW(target, InputNative.WM_MOUSEHWHEEL, WheelWParam(forward ? amount : -amount), lParam);
        }
        else
        {
            // WM_MOUSEWHEEL: 正=上。forward(下/末尾)=負
            InputNative.PostMessageW(target, InputNative.WM_MOUSEWHEEL, WheelWParam(forward ? -amount : amount), lParam);
        }
    }

    /// <summary>ホイールデルタを wParam 上位ワードに格納する（下位ワードのキー状態は 0）。</summary>
    private static nint WheelWParam(int delta)
        => (nint)(uint)((uint)(ushort)(short)delta << 16);
}
