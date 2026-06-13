using Clemotius.Core;
using Clemotius.Core.Config;
using Clemotius.Core.Scroll;

namespace Clemotius.Tests;

public class ModifierScrollResolverTests
{
    private sealed record Mods(bool Shift = false, bool Ctrl = false, bool Alt = false, bool Win = false)
        : IModifierState;

    private static ModifierScrollResolver Resolver(ModifierScrollSettings s) => new(s);

    [Fact]
    public void EachSlot_MatchesExactCombination()
    {
        var s = new ModifierScrollSettings
        {
            Shift = "code:58",
            Ctrl = "none",
            CtrlShift = "code:57",
            Alt = "code:55",
            ShiftAlt = "code:50",
            CtrlAlt = "none",
        };
        var r = Resolver(s);
        Assert.Equal("code:58", r.ResolveBehavior(new Mods(Shift: true)));
        Assert.Equal("none", r.ResolveBehavior(new Mods(Ctrl: true)));
        Assert.Equal("code:57", r.ResolveBehavior(new Mods(Ctrl: true, Shift: true)));
        Assert.Equal("code:55", r.ResolveBehavior(new Mods(Alt: true)));
        Assert.Equal("code:50", r.ResolveBehavior(new Mods(Shift: true, Alt: true)));
        Assert.Equal("none", r.ResolveBehavior(new Mods(Ctrl: true, Alt: true)));
    }

    [Fact]
    public void CtrlShift_IsDistinctFromSingleCtrl()
    {
        var s = new ModifierScrollSettings { Ctrl = "code:58", CtrlShift = "none" };
        Assert.Equal("none", Resolver(s).ResolveBehavior(new Mods(Ctrl: true, Shift: true)));
    }

    [Fact]
    public void NoModifier_ReturnsNull()
    {
        var s = new ModifierScrollSettings { Ctrl = "code:58" };
        Assert.Null(Resolver(s).ResolveBehavior(new Mods()));
    }

    [Fact]
    public void UnsupportedCombination_CtrlShiftAlt_ReturnsNull()
    {
        var s = new ModifierScrollSettings { Ctrl = "code:58", CtrlShift = "code:57" };
        Assert.Null(Resolver(s).ResolveBehavior(new Mods(Ctrl: true, Shift: true, Alt: true)));
    }
}

public class ScrollCodeDecoderTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("passthrough")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("code:abc")]
    public void Decode_NoneOrInvalid_ReturnsNull(string? behavior)
    {
        Assert.Null(ScrollCodeDecoder.Decode(behavior, null));
    }

    [Theory]
    // 垂直スロット: 50=1行 .. 54=12行 / 55=ページ / 57=端
    [InlineData("code:50", ScrollOrientation.Vertical, ScrollUnit.Line, 1)]
    [InlineData("code:51", ScrollOrientation.Vertical, ScrollUnit.Line, 3)]
    [InlineData("code:52", ScrollOrientation.Vertical, ScrollUnit.Line, 6)]
    [InlineData("code:53", ScrollOrientation.Vertical, ScrollUnit.Line, 9)]
    [InlineData("code:54", ScrollOrientation.Vertical, ScrollUnit.Line, 12)]
    [InlineData("code:55", ScrollOrientation.Vertical, ScrollUnit.Page, 1)]
    [InlineData("code:57", ScrollOrientation.Vertical, ScrollUnit.Edge, 1)]
    public void Decode_VerticalSlot(string behavior, ScrollOrientation o, ScrollUnit u, int amount)
    {
        var a = ScrollCodeDecoder.Decode(behavior, ScrollOrientation.Vertical)!;
        Assert.Equal(o, a.Orientation);
        Assert.Equal(u, a.Unit);
        Assert.Equal(amount, a.Amount);
    }

    [Theory]
    // 水平スロット: 56=1列 / 57=3列 / 58=6列 / 61=ページ / 63=端
    [InlineData("code:56", ScrollUnit.Line, 1)]
    [InlineData("code:57", ScrollUnit.Line, 3)]
    [InlineData("code:58", ScrollUnit.Line, 6)]
    [InlineData("code:61", ScrollUnit.Page, 1)]
    [InlineData("code:63", ScrollUnit.Edge, 1)]
    public void Decode_HorizontalSlot(string behavior, ScrollUnit u, int amount)
    {
        var a = ScrollCodeDecoder.Decode(behavior, ScrollOrientation.Horizontal)!;
        Assert.Equal(ScrollOrientation.Horizontal, a.Orientation);
        Assert.Equal(u, a.Unit);
        Assert.Equal(amount, a.Amount);
    }

    [Fact]
    public void Decode_UnknownSlot_UsesValueRange()
    {
        // スロット不明(修飾キー)では 56 以上を水平、未満を垂直と近似
        Assert.Equal(ScrollOrientation.Horizontal, ScrollCodeDecoder.Decode("code:58", null)!.Orientation);
        Assert.Equal(ScrollOrientation.Vertical, ScrollCodeDecoder.Decode("code:53", null)!.Orientation);
    }

    [Fact]
    public void Decode_HorizontalKeyword_IsHorizontalLine()
    {
        var a = ScrollCodeDecoder.Decode("horizontal", null)!;
        Assert.Equal(ScrollOrientation.Horizontal, a.Orientation);
        Assert.Equal(ScrollUnit.Line, a.Unit);
    }
}
