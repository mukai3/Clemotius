using System.Diagnostics;
using Clemotius.Interop;

namespace Clemotius.Actions;

/// <summary>
/// ウィンドウハンドルから前面アプリのプロセス名（拡張子なし）を取得する。
/// 保護プロセス等で取得できない場合は null。
/// </summary>
internal static class ProcessNameResolver
{
    public static string? FromWindow(nint hwnd)
    {
        if (hwnd == 0)
            return null;

        uint tid = InputNative.GetWindowThreadProcessId(hwnd, out uint pid);
        if (tid == 0 || pid == 0)
            return null;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName; // 拡張子なし
        }
        catch (ArgumentException)
        {
            return null; // プロセスが終了済み
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
