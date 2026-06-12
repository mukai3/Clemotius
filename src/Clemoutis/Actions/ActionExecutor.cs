using System.Runtime.InteropServices;
using Clemoutis.Core.Actions;
using Clemoutis.Interop;

namespace Clemoutis.Actions;

/// <summary>
/// アクションを機構別に実行する副作用層。
/// - KeyAction: 対象ウィンドウへフォーカスを移し SendInput でキー送信
/// - AppCommandAction: SendMessageW(WM_APPCOMMAND)
/// - CloseAction: PostMessageW(WM_CLOSE)
/// </summary>
internal sealed class ActionExecutor
{
    public void Execute(GestureAction action, nint targetWindow)
    {
        switch (action)
        {
            case KeyAction key:
                SendKey(key.Stroke, targetWindow);
                break;
            case AppCommandAction cmd:
                SendAppCommand(cmd.Command, targetWindow);
                break;
            case CloseAction:
                if (targetWindow != 0)
                    InputNative.PostMessageW(targetWindow, InputNative.WM_CLOSE, 0, 0);
                break;
        }
    }

    private static void SendAppCommand(AppCommand command, nint targetWindow)
    {
        if (targetWindow == 0)
            return;
        // WM_APPCOMMAND: lParam 上位ワードにコマンド、wParam に対象ウィンドウ
        nint lParam = (nint)((int)command << 16);
        InputNative.SendMessageW(targetWindow, InputNative.WM_APPCOMMAND, targetWindow, lParam);
    }

    private static void SendKey(KeyStroke stroke, nint targetWindow)
    {
        // 対象ウィンドウのスレッドに入力をアタッチしてフォーカスを確実化する
        uint thisThread = InputNative.GetCurrentThreadId();
        uint targetThread = targetWindow != 0
            ? InputNative.GetWindowThreadProcessId(targetWindow, out _)
            : 0;

        bool attached = false;
        if (targetThread != 0 && targetThread != thisThread)
            attached = InputNative.AttachThreadInput(thisThread, targetThread, true);

        try
        {
            if (targetWindow != 0)
            {
                InputNative.SetForegroundWindow(targetWindow);
                WaitForForeground(targetWindow);
            }

            var inputs = BuildKeyInputs(stroke);
            InputNative.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<InputNative.INPUT>());
        }
        finally
        {
            if (attached)
                InputNative.AttachThreadInput(thisThread, targetThread, false);
        }
    }

    /// <summary>
    /// 対象が前面化するのを短時間待つ。前面化前に SendInput すると非アクティブ窓への
    /// キーが落ちることがあるため（最大 ~50ms、体感遅延なし）。
    /// </summary>
    private static void WaitForForeground(nint target)
    {
        for (int i = 0; i < 10; i++)
        {
            if (InputNative.GetForegroundWindow() == target)
                return;
            Thread.Sleep(5);
        }
    }

    private static InputNative.INPUT[] BuildKeyInputs(KeyStroke stroke)
    {
        // 修飾キー押下 → 主キー押下 → 主キー解放 → 修飾キー解放（逆順）
        var mods = new List<ushort>(4);
        if (stroke.Ctrl) mods.Add(0x11);  // VK_CONTROL
        if (stroke.Shift) mods.Add(0x10); // VK_SHIFT
        if (stroke.Alt) mods.Add(0x12);   // VK_MENU
        if (stroke.Win) mods.Add(0x5B);   // VK_LWIN

        var list = new List<InputNative.INPUT>(mods.Count * 2 + 2);
        foreach (var m in mods)
            list.Add(KeyInput(m, down: true));
        list.Add(KeyInput(stroke.VirtualKey, down: true));
        list.Add(KeyInput(stroke.VirtualKey, down: false));
        for (int i = mods.Count - 1; i >= 0; i--)
            list.Add(KeyInput(mods[i], down: false));

        return list.ToArray();
    }

    private static InputNative.INPUT KeyInput(ushort vk, bool down) => new()
    {
        type = InputNative.INPUT_KEYBOARD,
        u = new InputNative.INPUTUNION
        {
            ki = new InputNative.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = down ? 0 : InputNative.KEYEVENTF_KEYUP,
                dwExtraInfo = InputNative.ClemoutisSignature,
            },
        },
    };
}
