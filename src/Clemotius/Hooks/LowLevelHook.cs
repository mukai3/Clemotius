using System.ComponentModel;
using System.Runtime.InteropServices;
using Clemotius.Interop;

namespace Clemotius.Hooks;

/// <summary>
/// 低レベルフックの共通基盤。
///
/// WH_*_LL フックのコールバックはフックを設置したスレッドで呼ばれ、そのスレッドが
/// メッセージをディスパッチし続けていないと OS にフックを外される。UI スレッドの負荷の
/// 影響を受けないよう、専用スレッド上でフックを設置し独自メッセージループを回す。
///
/// コールバックは OS に応答時間を監視されるため、派生クラスの Handle は割り当てを避け
/// 即座に返すこと（重い処理は別スレッドへ逃がす）。
/// </summary>
internal abstract class LowLevelHook : IDisposable
{
    private readonly int _hookId;
    // GC に回収されないようデリゲートをフィールドで保持する（必須）
    private readonly NativeMethods.HookProc _proc;
    private readonly ManualResetEventSlim _ready = new(false);

    private Thread? _thread;
    private uint _threadId;
    private nint _handle;
    private volatile bool _installed;

    protected LowLevelHook(int hookId)
    {
        _hookId = hookId;
        _proc = Callback;
    }

    public bool IsInstalled => _installed;

    /// <summary>最後にイベントを受信した Environment.TickCount。生存監視用。</summary>
    public uint LastEventTick { get; private set; }

    public void Install()
    {
        if (_thread is { IsAlive: true })
            return;

        _ready.Reset();
        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = GetType().Name + "Thread",
        };
        _thread.Start();
        _ready.Wait(); // 設置完了（成否確定）まで待つ

        if (_handle == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Uninstall()
    {
        if (_thread is not { IsAlive: true })
            return;
        // 専用スレッドのメッセージループを終了させる → ループ末尾で UnhookWindowsHookEx
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, 0, 0);
        _thread.Join();
        _thread = null;
    }

    public void Reinstall()
    {
        Uninstall();
        Install();
    }

    private void ThreadProc()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _handle = NativeMethods.SetWindowsHookExW(
            _hookId, _proc, NativeMethods.GetModuleHandleW(null), 0);
        _installed = _handle != 0;
        LastEventTick = (uint)Environment.TickCount;
        _ready.Set(); // Install() のブロックを解除

        if (_handle == 0)
            return;

        // WM_QUIT が届くまでメッセージをディスパッチし続ける（フックの応答性維持）
        while (NativeMethods.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        NativeMethods.UnhookWindowsHookEx(_handle);
        _handle = 0;
        _installed = false;
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

    public void Dispose()
    {
        Uninstall();
        _ready.Dispose();
    }
}
