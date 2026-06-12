using Clemoutis.Core.Scroll;

namespace Clemoutis.Tests;

public class ScrollBarBandTests
{
    // 要素: (100,100) 500x400、垂直バー幅17、水平バー高17 → 帯幅は 17+4=21
    private static BandHit Hit(int x, int y, bool v = true, bool h = true)
        => ScrollBarBand.Hit(x, y, 100, 100, 500, 400, v, h, 17, 17);

    [Fact]
    public void RightEdgeBand_IsVertical()
    {
        Assert.Equal(BandHit.Vertical, Hit(599, 300));  // 右端ぎりぎり
        Assert.Equal(BandHit.Vertical, Hit(579, 300));  // 帯の内側境界 (600-21=579)
    }

    [Fact]
    public void BottomEdgeBand_IsHorizontal()
    {
        Assert.Equal(BandHit.Horizontal, Hit(300, 499, v: false));
        Assert.Equal(BandHit.Horizontal, Hit(300, 479, v: false)); // 500-21=479
    }

    [Fact]
    public void Center_IsNone()
    {
        Assert.Equal(BandHit.None, Hit(300, 300));
        Assert.Equal(BandHit.None, Hit(578, 300)); // 帯の1px外
        Assert.Equal(BandHit.None, Hit(300, 478, v: false));
    }

    [Fact]
    public void BottomRightCorner_PrefersVertical()
    {
        Assert.Equal(BandHit.Vertical, Hit(599, 499));
    }

    [Fact]
    public void NotScrollable_IsNone()
    {
        Assert.Equal(BandHit.None, Hit(599, 300, v: false, h: false));
        Assert.Equal(BandHit.None, Hit(300, 499, v: false, h: false));
        // 垂直のみ不可なら右端帯は None（水平帯には該当しない座標）
        Assert.Equal(BandHit.None, Hit(599, 300, v: false, h: true));
    }

    [Fact]
    public void OutsideElement_IsNone()
    {
        Assert.Equal(BandHit.None, Hit(601, 300)); // 右外
        Assert.Equal(BandHit.None, Hit(99, 300));  // 左外
        Assert.Equal(BandHit.None, Hit(599, 501)); // 下外
    }

    [Fact]
    public void EmptyRect_IsNone()
    {
        Assert.Equal(BandHit.None, ScrollBarBand.Hit(0, 0, 0, 0, 0, 0, true, true, 17, 17));
    }
}
