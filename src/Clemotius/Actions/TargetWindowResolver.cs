using Clemotius.Interop;

namespace Clemotius.Actions;

/// <summary>
/// アクションの送り先ウィンドウを決定する。ジェスチャー開始位置直下の
/// トップレベルウィンドウを基本とし、取得できなければ前面ウィンドウにフォールバック。
/// </summary>
internal static class TargetWindowResolver
{
    public static nint Resolve(int startX, int startY)
    {
        var pt = new NativeMethods.POINT { X = startX, Y = startY };
        nint hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd != 0)
        {
            nint root = InputNative.GetAncestor(hwnd, InputNative.GA_ROOT);
            if (root != 0)
                return root;
        }
        return InputNative.GetForegroundWindow();
    }
}
