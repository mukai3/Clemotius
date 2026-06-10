using System.Runtime.InteropServices;
using Clemoutis.Actions;
using Clemoutis.Core.Gestures;
using Clemoutis.Interop;

namespace Clemoutis.Gestures;

/// <summary>
/// 右ボタンドラッグのジェスチャーを認識して実行する副作用層。
/// プロファイル文脈は <see cref="IGestureContextProvider"/> から開始時に解決する
/// （設定リロードやプロセス別プロファイルに追随）。
///
/// データフロー（設計書）:
///   右DOWN → 保留（DOWN を飲み込む） → 移動でストローク確定 → 右UP で判定
///     一致     : アクション実行（右クリックは発生させない）
///     不一致   : 何もしない（誤爆防止）
///     ストローク無 : 飲み込んだ右DOWN/UP を再生して通常の右クリックを成立させる
/// </summary>
internal sealed class GestureEngine
{
    private readonly IGestureContextProvider _provider;
    private readonly ActionExecutor _executor;

    private StrokeEncoder? _encoder;
    private GestureMatcher? _matcher;
    private bool _pending;
    private int _startX;
    private int _startY;

    public GestureEngine(IGestureContextProvider provider, ActionExecutor executor)
    {
        _provider = provider;
        _executor = executor;
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouse(int message, NativeMethods.MSLLHOOKSTRUCT data)
    {
        switch (message)
        {
            case NativeMethods.WM_RBUTTONDOWN:
                return OnRightDown(data.pt.X, data.pt.Y);

            case NativeMethods.WM_MOUSEMOVE:
                if (_pending)
                    _encoder!.Add(data.pt.X, data.pt.Y);
                return false;

            case NativeMethods.WM_RBUTTONUP:
                if (!_pending)
                    return false;
                _pending = false;
                return HandleRelease();

            default:
                if (_pending && IsButtonEvent(message))
                {
                    _pending = false;
                    _encoder?.Reset();
                }
                return false;
        }
    }

    private bool OnRightDown(int x, int y)
    {
        var ctx = _provider.Resolve(x, y);
        if (ctx is null || !ctx.Enabled)
            return false; // 対象外: 通常の右クリックとして素通し

        _matcher = ctx.Matcher;
        _pending = true;
        _startX = x;
        _startY = y;
        _encoder = new StrokeEncoder(Math.Max(1, _provider.Range));
        _encoder.Begin(x, y);
        return true; // DOWN を保留（飲み込む）
    }

    private bool HandleRelease()
    {
        if (_encoder!.HasStrokes)
        {
            var action = _matcher!.Match(_encoder.ToStrokeString());
            _encoder.Reset();
            if (action is not null)
            {
                nint target = TargetWindowResolver.Resolve(_startX, _startY);
                _executor.Execute(action, target);
                return true; // メニューを出さない
            }
            return true; // 不一致: 何もしない（誤爆防止）
        }

        // ストローク無し: 通常の右クリックを成立させる
        _encoder.Reset();
        ReplayRightClick();
        return true;
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
