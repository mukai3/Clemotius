using Clemoutis.Core;
using Clemoutis.Core.Config;
using Clemoutis.Core.Scroll;

namespace Clemoutis.Tests;

public class ModifierScrollResolverTests
{
    private sealed record Mods(bool Shift = false, bool Ctrl = false, bool Alt = false, bool Win = false)
        : IModifierState;

    private static ModifierScrollResolver Resolver(ModifierScrollSettings s) => new(s);

    [Fact]
    public void EachSingleAndPairSlot_MatchesExactCombination()
    {
        var s = new ModifierScrollSettings
        {
            Shift = "horizontal",
            Ctrl = "none",
            CtrlShift = "horizontal",
            Alt = "none",
            ShiftAlt = "horizontal",
            CtrlAlt = "none",
        };
        var r = Resolver(s);
        Assert.Equal(WheelConversion.Horizontal, r.Resolve(new Mods(Shift: true)));
        Assert.Equal(WheelConversion.None, r.Resolve(new Mods(Ctrl: true)));
        Assert.Equal(WheelConversion.Horizontal, r.Resolve(new Mods(Ctrl: true, Shift: true)));
        Assert.Equal(WheelConversion.None, r.Resolve(new Mods(Alt: true)));
        Assert.Equal(WheelConversion.Horizontal, r.Resolve(new Mods(Shift: true, Alt: true)));
        Assert.Equal(WheelConversion.None, r.Resolve(new Mods(Ctrl: true, Alt: true)));
    }

    [Fact]
    public void CtrlShift_IsDistinctFromSingleCtrl()
    {
        var s = new ModifierScrollSettings { Ctrl = "horizontal", CtrlShift = "none" };
        Assert.Equal(WheelConversion.None, Resolver(s).Resolve(new Mods(Ctrl: true, Shift: true)));
    }

    [Fact]
    public void NoModifier_ReturnsNone()
    {
        var s = new ModifierScrollSettings { Ctrl = "horizontal" };
        Assert.Equal(WheelConversion.None, Resolver(s).Resolve(new Mods()));
    }

    [Fact]
    public void UnsupportedCombination_CtrlShiftAlt_ReturnsNone()
    {
        var s = new ModifierScrollSettings
        {
            Ctrl = "horizontal", CtrlShift = "horizontal", CtrlAlt = "horizontal",
        };
        Assert.Equal(WheelConversion.None,
            Resolver(s).Resolve(new Mods(Ctrl: true, Shift: true, Alt: true)));
    }

    [Fact]
    public void DefaultSettings_AltIsUnknownCode_ResolvesNoneUntilDecoded()
    {
        // 既定の Alt="code:55" は意味未確定のため素通し（None）
        var r = new ModifierScrollResolver(new ModifierScrollSettings());
        Assert.Equal(WheelConversion.None, r.Resolve(new Mods(Alt: true)));
        Assert.Equal(WheelConversion.None, r.Resolve(new Mods(Ctrl: true)));
    }
}

public class ScrollBehaviorParserTests
{
    [Theory]
    [InlineData("horizontal", WheelConversion.Horizontal)]
    [InlineData("Horizontal", WheelConversion.Horizontal)]
    [InlineData("none", WheelConversion.None)]
    [InlineData("passthrough", WheelConversion.None)]
    [InlineData("", WheelConversion.None)]
    [InlineData(null, WheelConversion.None)]
    public void Parse_MapsBehaviors(string? behavior, WheelConversion expected)
    {
        Assert.Equal(expected, ScrollBehaviorParser.Parse(behavior));
    }

    [Theory]
    // 動的解析で確定したコードの方向ビット（bit3=0x08: 1=水平/0=垂直）
    [InlineData("code:58", WheelConversion.Horizontal)] // 水平既定/Shift
    [InlineData("code:57", WheelConversion.Horizontal)] // 水平3列
    [InlineData("code:53", WheelConversion.None)]       // 垂直既定/Ctrl
    [InlineData("code:50", WheelConversion.None)]       // 垂直1行
    [InlineData("code:55", WheelConversion.None)]       // 垂直（bit3立たず）
    [InlineData("code:abc", WheelConversion.None)]      // 不正コードは素通し
    public void Parse_DecodesOriginalCodeDirectionBit(string behavior, WheelConversion expected)
    {
        Assert.Equal(expected, ScrollBehaviorParser.Parse(behavior));
    }
}
