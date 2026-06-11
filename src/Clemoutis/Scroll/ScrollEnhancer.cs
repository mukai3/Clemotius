using System.Runtime.InteropServices;
using Clemoutis.Core;
using Clemoutis.Core.Config;
using Clemoutis.Core.Scroll;
using Clemoutis.Interop;

namespace Clemoutis.Scroll;

/// <summary>
/// ホイールイベントの拡張処理。v1 スコープ:
///   1. 修飾キー押下中の変換（設定 scroll.modifierRules、既定は Alt）
///   2. 水平スクロールバー上での縦ホイール → 水平スクロール
/// どちらにも該当しなければ素通しする。
/// </summary>
internal sealed class ScrollEnhancer
{
    private readonly IModifierState _modifiers;
    private volatile ScrollSettings _settings;
    private volatile ModifierScrollResolver _resolver;

    public ScrollEnhancer(IModifierState modifiers, ScrollSettings settings)
    {
        _modifiers = modifiers;
        _settings = settings;
        _resolver = new ModifierScrollResolver(settings.ModifierScroll);
    }

    public void UpdateSettings(ScrollSettings settings)
    {
        _settings = settings;
        _resolver = new ModifierScrollResolver(settings.ModifierScroll);
    }

    /// <returns>true ならイベントを飲み込む</returns>
    public bool OnMouseWheel(NativeMethods.MSLLHOOKSTRUCT data)
    {
        // mouseData 上位ワードが符号付きホイールデルタ
        short delta = (short)(data.mouseData >> 16);
        if (delta == 0)
            return false;

        // 1) 修飾キー変換が優先
        var conversion = _resolver.Resolve(_modifiers);

        // 2) 修飾キーなし時はスクロールバー上判定（向きごとの挙動）
        if (conversion == WheelConversion.None)
        {
            var hit = ScrollBarDetector.Detect(data.pt.X, data.pt.Y);
            conversion = hit switch
            {
                ScrollBarHit.Horizontal => ScrollBehaviorParser.Parse(_settings.OnHorizontalScrollbar),
                ScrollBarHit.Vertical => ScrollBehaviorParser.Parse(_settings.OnVerticalScrollbar),
                _ => WheelConversion.None,
            };
        }

        if (conversion == WheelConversion.Horizontal)
        {
            SendHorizontalWheel(delta);
            return true; // 元の縦ホイールは飲み込む
        }

        return false; // 素通し
    }

    private static void SendHorizontalWheel(short verticalDelta)
    {
        // 縦ホイール上(+)を左方向、下(-)を右方向に対応づける。
        // MOUSEEVENTF_HWHEEL は正で右方向のため符号を反転する。
        var input = new InputNative.INPUT
        {
            type = InputNative.INPUT_MOUSE,
            u = new InputNative.INPUTUNION
            {
                mi = new InputNative.MOUSEINPUT
                {
                    mouseData = unchecked((uint)(-verticalDelta)),
                    dwFlags = InputNative.MOUSEEVENTF_HWHEEL,
                    dwExtraInfo = InputNative.ClemoutisSignature,
                },
            },
        };
        InputNative.SendInput(1, new[] { input }, Marshal.SizeOf<InputNative.INPUT>());
    }
}
