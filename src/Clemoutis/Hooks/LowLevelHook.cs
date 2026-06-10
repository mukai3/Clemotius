using System.ComponentModel;
using System.Runtime.InteropServices;
using Clemoutis.Interop;

namespace Clemoutis.Hooks;

/// <summary>
/// 低レベルフックの共通基盤。コールバックは OS に応答時間を監視されるため、
/// 派生クラスの Handle は割り当てを避け即座に返すこと。
/// </summary>
internal abstract class LowLevelHook : IDisposable
{
    private readonly int _hookId;
    // GC に回収されないようデリゲートをフィールドで保持する（必須）
    private readonly NativeMethods.HookProc _proc;
    private nint _handle;

    protected LowLevelHook(int hookId)
    {
        _hookId = hookId;
        _proc = Callback;
    }

    public bool IsInstalled => _handle != 0;

    /// <summary>最後にイベントを受信した Environment.TickCount。生存監視用。</summary>
    public uint LastEventTick { get; private set; }

    public void Install()
    {
        if (_handle != 0) return;
        _handle = NativeMethods.SetWindowsHookExW(
            _hookId, _proc, NativeMethods.GetModuleHandleW(null), 0);
        if (_handle == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        LastEventTick = (uint)Environment.TickCount;
    }

    public void Uninstall()
    {
        if (_handle == 0) return;
        NativeMethods.UnhookWindowsHookEx(_handle);
        _handle = 0;
    }

    public void Reinstall()
    {
        Uninstall();
        Install();
    }

    private nint Callback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            LastEventTick = (uint)Environment.TickCount;
            if (Handle(wParam, lParam))
                return 1; // イベントを飲み込む
        }
        return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    /// <returns>true を返すとイベントを飲み込む（後続へ渡さない）</returns>
    protected abstract bool Handle(nint wParam, nint lParam);

    public void Dispose() => Uninstall();
}
