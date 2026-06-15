namespace Clemotius.Core.Diagnostics;

/// <summary>
/// 低レベルフックの生死判定のうち、Win32 非依存の純粋ロジック。
/// マウスフックは別途「能動プローブ（0移動注入が届くか）」で確定判定する前提で、
/// ここではキーボードフックの生死を 2 デバイス前提の演繹で推定する。
/// </summary>
public static class HookLiveness
{
    /// <summary>
    /// ラップアラウンド安全な経過時間（ms）。tick は GetTickCount / Environment.TickCount 由来。
    /// </summary>
    public static uint Elapsed(uint nowTick, uint thenTick) => unchecked(nowTick - thenTick);

    /// <summary>
    /// キーボードフックが死んでいる可能性が高いかを推定する。
    ///
    /// 前提: 入力デバイスはマウスとキーボードの 2 種。マウスフックは能動プローブで
    /// 生存が確認できている（<paramref name="mouseConfirmedAlive"/>）。
    ///
    /// 判定: システム全体には最近入力があった（<paramref name="systemInputTick"/> が新しい）のに、
    /// 実マウス入力は無く（<paramref name="mouseRealEventTick"/> が古い）、かつキーボードフックも
    /// イベントを受けていない（<paramref name="keyboardEventTick"/> が古い）なら、
    /// 「最近の入力はキーボードだったのにキーボードフックが取りこぼした＝死亡」と推定する。
    /// </summary>
    public static bool KeyboardLikelyDead(
        uint nowTick,
        uint systemInputTick,
        uint keyboardEventTick,
        uint mouseRealEventTick,
        uint staleThresholdMs,
        bool mouseConfirmedAlive)
    {
        if (!mouseConfirmedAlive)
            return false; // マウス側が不確定なら巻き込み誤判定を避ける

        bool systemRecent = Elapsed(nowTick, systemInputTick) < staleThresholdMs;
        bool keyboardStale = Elapsed(nowTick, keyboardEventTick) >= staleThresholdMs;
        bool mouseRealStale = Elapsed(nowTick, mouseRealEventTick) >= staleThresholdMs;

        return systemRecent && keyboardStale && mouseRealStale;
    }
}
