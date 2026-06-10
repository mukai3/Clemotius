using System.Runtime.InteropServices;
using Clemoutis.Hooks;
using Clemoutis.Interop;

namespace Clemoutis;

/// <summary>
/// フックの生存監視。低レベルフックはコールバックの応答遅延で OS に外されることが
/// あるため、「システムには入力が届いているのにフックへ届いていない」状態を検知して
/// 自動再設置する。
/// </summary>
internal sealed class HookWatchdog : IDisposable
{
    // システム入力よりフック受信がこれ以上古ければ「外された」とみなす
    private const uint StaleThresholdMs = 10_000;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly LowLevelHook[] _hooks;
    private readonly Action _onReinstalled;

    public HookWatchdog(LowLevelHook[] hooks, Action onReinstalled)
    {
        _hooks = hooks;
        _onReinstalled = onReinstalled;
        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    private void Check()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
        };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return;

        bool reinstalled = false;
        foreach (var hook in _hooks)
        {
            if (!hook.IsInstalled)
                continue; // 一時停止中
            // TickCount のラップアラウンドを考慮した差分
            uint sinceHook = info.dwTime - hook.LastEventTick;
            if (sinceHook < int.MaxValue && sinceHook > StaleThresholdMs)
            {
                hook.Reinstall();
                reinstalled = true;
            }
        }
        if (reinstalled)
            _onReinstalled();
    }

    public void Dispose() => _timer.Dispose();
}
