using System.Runtime.InteropServices;
using Clemoutis.Actions;
using Clemoutis.Core.Gestures;
using Clemoutis.Interop;

namespace Clemoutis.Gestures;

/// <summary>
/// 右ボタンドラッグのジェスチャーを認識して実行する副作用層。
/// データフロー（設計書）:
///   右DOWN → 保留（DOWN を飲み込む） → 移動でストローク確定 → 右UP で判定
///     一致     : アクション実行（右クリックは発生させない）
///     不一致   : 何もしない（誤爆防止）
///     ストローク無 : 飲み込んだ右DOWN/UP を再生して通常の右クリックを成立させる
/// </summary>
internal sealed class GestureEngine
{
    private readonly StrokeEncoder _encoder;
    private readonly GestureMatcher _matcher;
    private readonly ActionExecutor _executor;

    private bool _pending;
    private int _startX;
    private int _startY;

    public GestureEngine(StrokeEncoder encoder, GestureMatcher matcher, ActionExecutor executor)
    {
        _encoder = encoder;
        _matcher = matcher;
        _executor = executor;
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouse(int message, NativeMethods.MSLLHOOKSTRUCT data)
    {
        switch (message)
        {
            case NativeMethods.WM_RBUTTONDOWN:
                _pending = true;
                _startX = data.pt.X;
                _startY = data.pt.Y;
                _encoder.Begin(data.pt.X, data.pt.Y);
                return true; // DOWN を保留（飲み込む）

            case NativeMethods.WM_MOUSEMOVE:
                if (_pending)
                    _encoder.Add(data.pt.X, data.pt.Y);
                return false; // 移動自体は素通しでよい

            case NativeMethods.WM_RBUTTONUP:
                if (!_pending)
                    return false;
                _pending = false;
                return HandleRelease();

            default:
                // ジェスチャー中に他ボタンが来たらキャンセルして素通し
                if (_pending && IsButtonEvent(message))
                {
                    _pending = false;
                    _encoder.Reset();
                }
                return false;
        }
    }

    private bool HandleRelease()
    {
        if (_encoder.HasStrokes)
        {
            var action = _matcher.Match(_encoder.ToStrokeString());
            _encoder.Reset();
            if (action is not null)
            {
                nint target = TargetWindowResolver.Resolve(_startX, _startY);
                _executor.Execute(action, target);
                return true; // 右UP を飲み込む（メニューを出さない）
            }
            // 不一致: 何もしない。UP は飲み込み、保留した DOWN は再生しない（誤爆防止）
            return true;
        }

        // ストローク無し: 通常の右クリックを成立させる（保留した DOWN ＋ UP を再生）
        _encoder.Reset();
        ReplayRightClick();
        return true; // 元の UP は飲み込み、再生した一組で右クリックを成立させる
    }

    private static bool IsButtonEvent(int message) => message
        is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_LBUTTONUP
        or NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_MBUTTONUP
        or NativeMethods.WM_XBUTTONDOWN or NativeMethods.WM_XBUTTONUP;

    private static void ReplayRightClick()
    {
        var inputs = new[]
        {
            RightButton(InputNative.MOUSEEVENTF_RIGHTDOWN),
            RightButton(InputNative.MOUSEEVENTF_RIGHTUP),
        };
        InputNative.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<InputNative.INPUT>());
    }

    private static InputNative.INPUT RightButton(uint flag) => new()
    {
        type = InputNative.INPUT_MOUSE,
        u = new InputNative.INPUTUNION
        {
            mi = new InputNative.MOUSEINPUT
            {
                dwFlags = flag,
                dwExtraInfo = InputNative.ClemoutisSignature,
            },
        },
    };
}
