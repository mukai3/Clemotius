using System.Runtime.InteropServices;
using Clemotius.Actions;
using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;
using Clemotius.Interop;

namespace Clemotius.Gestures;

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

    // フックコールバック（フックスレッド）とタイムアウトタイマー（別スレッド）から
    // 共有状態を触るためロックで保護する。
    private readonly object _gate = new();
    private StrokeEncoder? _encoder;
    private GestureMatcher? _matcher;
    private GestureAction? _wheelUp;
    private GestureAction? _wheelDown;
    private System.Threading.Timer? _timeoutTimer;
    private volatile bool _pending;
    private bool _wheelUsed;
    private volatile bool _gestureCancelled; // タイムアウト中断済み→次の RBUTTONUP を飲み込む
    private int _startX;
    private int _startY;

    // 軌跡描画用イベント（フックスレッドから発火する。購読側で UI スレッドへマーシャルすること）
    public event Action<int, int>? GestureStarted;
    public event Action<int, int>? GesturePoint;
    public event Action? GestureEnded;
    // ストローク確定ごとに、現在のストローク列とマッチしたアクション（無ければ null）を通知
    public event Action<string, GestureAction?>? GestureProgress;

    public GestureEngine(IGestureContextProvider provider, ActionExecutor executor)
    {
        _provider = provider;
        _executor = executor;
    }

    // アクション実行はフックコールバックを軽く保つため別タスクへ逃がす
    // （SendMessage 等がブロックしてもフックが OS に外されないようにする）
    private void ExecuteAsync(GestureAction action, nint target)
        => Task.Run(() => _executor.Execute(action, target));

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouse(int message, NativeMethods.MSLLHOOKSTRUCT data)
    {
        switch (message)
        {
            case NativeMethods.WM_RBUTTONDOWN:
                return OnRightDown(data.pt.X, data.pt.Y);

            case NativeMethods.WM_MOUSEMOVE:
                if (_pending)
                    OnMove(data.pt.X, data.pt.Y);
                return false;

            case NativeMethods.WM_MOUSEWHEEL:
                return _pending && OnWheelWhilePending(data);

            case NativeMethods.WM_RBUTTONUP:
                return OnRightUp();

            default:
                if (_pending && IsButtonEvent(message))
                {
                    lock (_gate)
                    {
                        _pending = false;
                        CancelTimeoutLocked();
                        _encoder?.Reset();
                    }
                    GestureEnded?.Invoke();
                }
                return false;
        }
    }

    private bool OnRightDown(int x, int y)
    {
        var ctx = _provider.Resolve(x, y);
        if (ctx is null || !ctx.Enabled)
            return false; // 対象外: 通常の右クリックとして素通し

        lock (_gate)
        {
            _matcher = ctx.Matcher;
            _wheelUp = ctx.WheelUp;
            _wheelDown = ctx.WheelDown;
            _wheelUsed = false;
            _gestureCancelled = false;
            _startX = x;
            _startY = y;
            _encoder = new StrokeEncoder(Math.Max(1, _provider.Range));
            _encoder.Begin(x, y);
            _pending = true;
            StartTimeoutLocked();
        }
        GestureStarted?.Invoke(x, y);
        return true; // DOWN を保留（飲み込む）
    }

    private void OnMove(int x, int y)
    {
        if (!_pending)
            return;
        string? strokes = null;
        GestureAction? match = null;
        lock (_gate)
        {
            if (_pending && _encoder is not null && _encoder.Add(x, y))
            {
                CancelTimeoutLocked(); // 最初のストローク確定でタイムアウト不要
                strokes = _encoder.ToStrokeString();
                match = _matcher?.Match(strokes);
            }
        }
        GesturePoint?.Invoke(x, y);
        if (strokes is not null)
            GestureProgress?.Invoke(strokes, match);
    }

    /// <summary>右ボタン押下中のホイール回転を右+ホイールジェスチャーとして処理する。</summary>
    private bool OnWheelWhilePending(NativeMethods.MSLLHOOKSTRUCT data)
    {
        short delta = (short)(data.mouseData >> 16);
        if (delta == 0)
            return false;

        GestureAction? action;
        int sx, sy;
        lock (_gate)
        {
            if (!_pending)
                return false;
            action = delta > 0 ? _wheelUp : _wheelDown;
            if (action is null)
                return false; // 割当無し: 通常スクロールとして素通し
            _wheelUsed = true; // UP 時にメニューを出さない／右クリックを再生しない
            CancelTimeoutLocked();
            sx = _startX;
            sy = _startY;
        }
        ExecuteAsync(action, TargetWindowResolver.Resolve(sx, sy));
        return true; // 通常スクロールを抑制
    }

    private bool OnRightUp()
    {
        GestureAction? action = null;
        bool replay = false;
        int sx = 0, sy = 0;

        lock (_gate)
        {
            if (_gestureCancelled)
            {
                _gestureCancelled = false;
                return true; // タイムアウトで中断済み→再生済みなので UP を飲み込む
            }
            if (!_pending)
                return false;
            _pending = false;
            CancelTimeoutLocked();

            bool wheelUsed = _wheelUsed;
            _wheelUsed = false;
            sx = _startX;
            sy = _startY;

            if (_encoder!.HasStrokes)
            {
                action = _matcher!.Match(_encoder.ToStrokeString());
                _encoder.Reset();
                // 一致でも不一致でもメニューは出さない（不一致は誤爆防止で何もしない）
            }
            else
            {
                _encoder.Reset();
                // 右+ホイール使用済みは右クリックを成立させない（メニュー抑制）
                if (!wheelUsed)
                    replay = true;
            }
        }

        if (action is not null)
            ExecuteAsync(action, TargetWindowResolver.Resolve(sx, sy));
        if (replay)
        {
            // フックコールバック内から SendInput すると、注入イベントが自分の
            // LLフック（このスレッド）を通れず約300ms（LLフックタイムアウト）
            // 待たされ、コンテキストメニュー表示が遅延する。別スレッドで再生する。
            Task.Run(ReplayRightClick);
        }
        GestureEnded?.Invoke();
        return true;
    }

    // ── 入力タイムアウト（押下後ストローク無しなら通常の右クリックに戻す） ──

    private void StartTimeoutLocked()
    {
        _timeoutTimer?.Dispose();
        int ms = _provider.TimeoutMs;
        if (ms <= 0)
        {
            _timeoutTimer = null;
            return;
        }
        _timeoutTimer = new System.Threading.Timer(_ => OnTimeout(), null, ms, Timeout.Infinite);
    }

    private void CancelTimeoutLocked()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }

    private void OnTimeout()
    {
        bool replay = false;
        lock (_gate)
        {
            if (!_pending)
                return; // 既に確定/解放済み
            if (_encoder!.HasStrokes || _wheelUsed)
                return; // 入力が始まっている→タイムアウト無効
            _pending = false;
            _gestureCancelled = true; // 次の RBUTTONUP を飲み込む
            _encoder.Reset();
            CancelTimeoutLocked();
            replay = true;
        }
        if (replay)
            ReplayRightClick();
        GestureEnded?.Invoke();
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
                dwExtraInfo = InputNative.ClemotiusSignature,
            },
        },
    };

    /// <summary>
    /// 右+ホイール未割当時、保留していた右DOWNとこのホイールをアプリへ送り、
    /// アプリ独自の右+ホイール動作を発動させる。フックスレッドを塞がないよう別スレッドで、
    /// down → wheel の順序を保って注入する（順序が逆だとアプリが右押下を認識できない）。
    /// この後ユーザーが実際に離す右UPは _pending=false により素通しされ、注入 down と対になる。
    /// </summary>
    private static void ReleaseRightWheelToApp(short delta)
    {
        Task.Run(() =>
        {
            var down = new[] { RightButton(InputNative.MOUSEEVENTF_RIGHTDOWN) };
            InputNative.SendInput(1, down, Marshal.SizeOf<InputNative.INPUT>());
            var wheel = new[] { WheelInput(delta) };
            InputNative.SendInput(1, wheel, Marshal.SizeOf<InputNative.INPUT>());
        });
    }

    private static InputNative.INPUT WheelInput(short delta) => new()
    {
        type = InputNative.INPUT_MOUSE,
        u = new InputNative.INPUTUNION
        {
            mi = new InputNative.MOUSEINPUT
            {
                // MOUSEEVENTF_WHEEL では mouseData が回転量（WHEEL_DELTA=120 単位、符号付き）
                mouseData = (uint)delta,
                dwFlags = InputNative.MOUSEEVENTF_WHEEL,
                dwExtraInfo = InputNative.ClemotiusSignature,
            },
        },
    };
}
