using System.Runtime.InteropServices;
using Clemotius.Core.Actions;
using Clemotius.Interop;

namespace Clemotius.Actions;

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

    // SendKey は Task.Run から並行に呼ばれ得る（高速なホイールジェスチャ等）。
    // アタッチ/デタッチはキュー共有のキー状態をリセットするため、並行実行すると
    // 他方の修飾キー押下が消えて主キーだけ届くことがある。直列化して防ぐ。
    private static readonly object SendKeyGate = new();

    private static void SendKey(KeyStroke stroke, nint targetWindow)
    {
        lock (SendKeyGate)
            SendKeyCore(stroke, targetWindow);
    }

    private static void SendKeyCore(KeyStroke stroke, nint targetWindow)
    {
        if (targetWindow != 0)
        {
            BringToForeground(targetWindow);
            WaitForForeground(targetWindow);
        }

        // 注入はどのスレッドとも結合していない状態で行う。AttachThreadInput の
        // 結合/解除は共有キューのキー状態をリセットするため、対象スレッドと
        // 結合したまま注入すると、処理前のリセットで修飾キーだけが欠落する
        // （Ctrl+T が T になる）。
        var inputs = BuildKeyInputs(stroke);
        InputNative.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<InputNative.INPUT>());
    }

    /// <summary>
    /// 対象を前面化する。バックグラウンドプロセスの SetForegroundWindow は
    /// 通常拒否されるため、「現在の前面ウィンドウのスレッド」と一時的に結合して
    /// 前面化権限を得る。結合相手は対象ではなく旧前面側なので、結合/解除に伴う
    /// キー状態リセットは対象のキューに影響しない。
    /// </summary>
    private static void BringToForeground(nint target)
    {
        nint fg = InputNative.GetForegroundWindow();
        if (fg == target)
            return; // 既に前面

        uint thisThread = InputNative.GetCurrentThreadId();
        uint fgThread = fg != 0 ? InputNative.GetWindowThreadProcessId(fg, out _) : 0;
        bool attached = fgThread != 0 && fgThread != thisThread
            && InputNative.AttachThreadInput(thisThread, fgThread, true);
        try
        {
            InputNative.SetForegroundWindow(target);
        }
        finally
        {
            if (attached)
                InputNative.AttachThreadInput(thisThread, fgThread, false);
        }
    }

    /// <summary>
    /// 対象が前面化するのを短時間待つ。前面化前に SendInput すると修飾キーと主キーが
    /// 別ウィンドウへ割れて届くことがあるため（最大 ~100ms、体感遅延なし）。
    /// </summary>
    private static void WaitForForeground(nint target)
    {
        for (int i = 0; i < 20; i++)
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
                dwExtraInfo = InputNative.ClemotiusSignature,
            },
        },
    };
}
