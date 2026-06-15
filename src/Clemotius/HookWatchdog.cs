using System.Runtime.InteropServices;
using Clemotius.Core.Diagnostics;
using Clemotius.Hooks;
using Clemotius.Interop;

namespace Clemotius;

/// <summary>
/// フックの生存監視。低レベルフックはコールバックの応答遅延で OS に外されることが
/// あるため、外れたフックを検知して自動再設置する。
///
/// 検出はデバイスごとに分けて行う（システム全体の最終入力時刻 GetLastInputInfo は
/// デバイス種別を区別できず、片方のデバイスだけ使うと他方フックを誤って stale 判定して
/// しまうため）:
///   - マウス: 0 移動イベントを自前注入し（カーソルは動かない）、フックが受け取って
///     <see cref="LowLevelHook.LastEventTick"/> が進むかで生死を確定判定する。
///   - キーボード: 合成入力の副作用を避けるため注入はせず、「マウスは生存／実マウス入力なし／
///     システム入力は最近あった／キーボードフックは沈黙」の演繹で死亡を推定する
///     （<see cref="HookLiveness.KeyboardLikelyDead"/>）。
/// </summary>
internal sealed class HookWatchdog : IDisposable
{
    // システム入力よりフック受信がこれ以上古ければ「外された」とみなす
    private const uint StaleThresholdMs = 10_000;
    private const int CheckIntervalMs = 30_000;
    private const int ProbeVerifyDelayMs = 250; // 注入が届くのを待つ猶予
    private const int RequiredMissStreak = 2;   // 連続でこの回数不達なら死亡と判断

    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _probeVerify;
    private readonly MouseHook _mouse;
    private readonly KeyboardHook _keyboard;
    private readonly Action _onReinstalled;

    private uint _probeBaseline;   // 注入直前のマウス LastEventTick
    private bool _probeInFlight;   // マウスプローブ送出済み・確認待ち
    private bool _mouseAliveThisCycle; // 直近の確認でマウス生存が取れたか
    private int _mouseMissStreak;  // 連続でプローブが不達だった回数

    public HookWatchdog(MouseHook mouse, KeyboardHook keyboard, Action onReinstalled)
    {
        _mouse = mouse;
        _keyboard = keyboard;
        _onReinstalled = onReinstalled;

        _probeVerify = new System.Windows.Forms.Timer { Interval = ProbeVerifyDelayMs };
        _probeVerify.Tick += (_, _) => { _probeVerify.Stop(); VerifyMouseProbe(); };

        _timer = new System.Windows.Forms.Timer { Interval = CheckIntervalMs };
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    private void Check()
    {
        // 前周期のプローブが確認待ちのままなら、この周期はスキップ（多重注入を避ける）
        if (_probeInFlight)
            return;

        if (_mouse.IsInstalled)
            StartMouseProbe();
        else
            _mouseAliveThisCycle = false;
    }

    // ── マウス: 能動プローブ ──

    private void StartMouseProbe()
    {
        _probeBaseline = _mouse.LastEventTick;
        if (!InjectNoOpMouseMove())
        {
            // 注入できない（セキュアデスクトップ/UIPI 等）。生死は判定不能なので
            // 再設置せず、この周期のマウス生存も不確定にする。
            _mouseAliveThisCycle = false;
            return;
        }
        _probeInFlight = true;
        _probeVerify.Start();
    }

    private void VerifyMouseProbe()
    {
        _probeInFlight = false;

        // LastEventTick が進んでいれば（自前注入か実入力を受けた）フックは生存。
        // さらに、実ユーザーのマウス入力が最近あった場合も生存とみなす。これにより、
        // 万一 0 移動注入が環境差でフックに届かなくても、実入力で誤って死亡判定しない。
        bool advanced = _mouse.LastEventTick != _probeBaseline;
        bool realRecent = HookLiveness.Elapsed(
            (uint)Environment.TickCount, _mouse.LastRealEventTick) < StaleThresholdMs;
        bool alive = advanced || realRecent;

        bool reinstalled = false;
        if (alive)
        {
            _mouseMissStreak = 0;
            _mouseAliveThisCycle = true;
        }
        else if (++_mouseMissStreak >= RequiredMissStreak)
        {
            // 連続で注入が届かない＝フックが外れたと判断して再設置
            _mouse.Reinstall();
            _mouseMissStreak = 0;
            _mouseAliveThisCycle = true; // 再設置したので以降は生存扱い
            reinstalled = true;
        }
        else
        {
            // 1 回の不達は判定保留（この周期はキーボード推論も行わない）
            _mouseAliveThisCycle = false;
        }

        // マウス生存が確定した上でキーボードの生死を推論する
        if (CheckKeyboard())
            reinstalled = true;

        if (reinstalled)
            _onReinstalled();
    }

    // ── キーボード: 演繹による推論（注入なし） ──

    private bool CheckKeyboard()
    {
        if (!_keyboard.IsInstalled)
            return false;

        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
        };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return false;

        bool dead = HookLiveness.KeyboardLikelyDead(
            nowTick: (uint)Environment.TickCount,
            systemInputTick: info.dwTime,
            keyboardEventTick: _keyboard.LastEventTick,
            mouseRealEventTick: _mouse.LastRealEventTick,
            staleThresholdMs: StaleThresholdMs,
            mouseConfirmedAlive: _mouseAliveThisCycle);

        if (!dead)
            return false;

        _keyboard.Reinstall();
        return true;
    }

    /// <summary>カーソルを動かさない 0 移動イベントを自前署名つきで注入する。</summary>
    private static bool InjectNoOpMouseMove()
    {
        var inputs = new[]
        {
            new InputNative.INPUT
            {
                type = InputNative.INPUT_MOUSE,
                u = new InputNative.INPUTUNION
                {
                    mi = new InputNative.MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        dwFlags = InputNative.MOUSEEVENTF_MOVE,
                        dwExtraInfo = InputNative.ClemotiusSignature,
                    },
                },
            },
        };
        return InputNative.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<InputNative.INPUT>()) != 0;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _probeVerify.Dispose();
    }
}
