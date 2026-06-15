using Clemotius.Core.Diagnostics;

namespace Clemotius.Tests;

public class HookLivenessTests
{
    private const uint Threshold = 10_000;
    private const uint Now = 1_000_000;

    // 最近の入力がキーボード由来（実マウスなし）でキーボードフックが沈黙 → 死亡と推定
    [Fact]
    public void KeyboardLikelyDead_RecentSystemInput_NoMouse_KeyboardSilent()
    {
        bool dead = HookLiveness.KeyboardLikelyDead(
            nowTick: Now,
            systemInputTick: Now - 1_000,        // 1秒前に入力あり
            keyboardEventTick: Now - 60_000,     // キーボードフックは沈黙
            mouseRealEventTick: Now - 60_000,    // 実マウスも無し
            staleThresholdMs: Threshold,
            mouseConfirmedAlive: true);
        Assert.True(dead);
    }

    // 最近の入力が実マウス由来なら、キーボード沈黙は単なる未使用 → 死亡としない
    [Fact]
    public void KeyboardNotDead_WhenRecentMouseInput()
    {
        bool dead = HookLiveness.KeyboardLikelyDead(
            nowTick: Now,
            systemInputTick: Now - 1_000,
            keyboardEventTick: Now - 60_000,
            mouseRealEventTick: Now - 1_000,     // 直近に実マウスあり
            staleThresholdMs: Threshold,
            mouseConfirmedAlive: true);
        Assert.False(dead);
    }

    // システム入力自体が古い（誰も使っていない）なら判定しない
    [Fact]
    public void KeyboardNotDead_WhenNoRecentSystemInput()
    {
        bool dead = HookLiveness.KeyboardLikelyDead(
            nowTick: Now,
            systemInputTick: Now - 60_000,
            keyboardEventTick: Now - 60_000,
            mouseRealEventTick: Now - 60_000,
            staleThresholdMs: Threshold,
            mouseConfirmedAlive: true);
        Assert.False(dead);
    }

    // キーボードフックが最近受けているなら生存
    [Fact]
    public void KeyboardNotDead_WhenKeyboardRecent()
    {
        bool dead = HookLiveness.KeyboardLikelyDead(
            nowTick: Now,
            systemInputTick: Now - 1_000,
            keyboardEventTick: Now - 1_000,
            mouseRealEventTick: Now - 60_000,
            staleThresholdMs: Threshold,
            mouseConfirmedAlive: true);
        Assert.False(dead);
    }

    // マウス生存が未確定なら巻き込み誤判定を避ける
    [Fact]
    public void KeyboardNotDead_WhenMouseNotConfirmed()
    {
        bool dead = HookLiveness.KeyboardLikelyDead(
            nowTick: Now,
            systemInputTick: Now - 1_000,
            keyboardEventTick: Now - 60_000,
            mouseRealEventTick: Now - 60_000,
            staleThresholdMs: Threshold,
            mouseConfirmedAlive: false);
        Assert.False(dead);
    }

    // TickCount ラップアラウンドをまたいでも経過時間が正しく出る
    [Fact]
    public void Elapsed_HandlesWrapAround()
    {
        // now が 0 付近、then がラップ前（uint 最大付近）
        Assert.Equal(10u, HookLiveness.Elapsed(5u, unchecked(5u - 10u)));
    }
}
