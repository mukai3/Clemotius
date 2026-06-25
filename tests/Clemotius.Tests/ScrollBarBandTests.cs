using Clemotius.Core.Scroll;

namespace Clemotius.Tests;

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
        // 横帯は HorizontalMargin=0 のため実バー厚のみ（500-17=483 が内側境界）
        Assert.Equal(BandHit.Horizontal, Hit(300, 499, v: false));
        Assert.Equal(BandHit.Horizontal, Hit(300, 483, v: false)); // 帯の内側境界 (500-17=483)
    }

    [Fact]
    public void HorizontalBand_IsTighterThanVertical()
    {
        // Web の横カルーセル下端での誤判定を抑えるため、横帯は余裕を持たせない。
        // 縦帯と同じ「下端から21px」の位置(479)は、横では帯外＝None になる。
        Assert.Equal(BandHit.None, Hit(300, 479, v: false)); // 旧仕様(Margin=4)では Horizontal だった
        Assert.Equal(BandHit.None, Hit(300, 482, v: false)); // 境界の1px外
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

    // EdgeCandidate: 窓 (100,100) 500x400、バー17 → 帯幅 17+4=21（スクロール可否は見ない）
    private static BandHit Edge(int x, int y)
        => ScrollBarBand.EdgeCandidate(x, y, 100, 100, 500, 400, 17, 17);

    [Fact]
    public void EdgeCandidate_RightEdge_IsVertical()
    {
        Assert.Equal(BandHit.Vertical, Edge(599, 300));  // 右端ぎりぎり
        Assert.Equal(BandHit.Vertical, Edge(579, 300));  // 帯の内側境界 (600-21=579)
    }

    [Fact]
    public void EdgeCandidate_BottomEdge_IsHorizontal()
    {
        Assert.Equal(BandHit.Horizontal, Edge(300, 499));
        Assert.Equal(BandHit.Horizontal, Edge(300, 479)); // 500-21=479
    }

    [Fact]
    public void EdgeCandidate_Center_IsNone()
    {
        Assert.Equal(BandHit.None, Edge(300, 300));
        Assert.Equal(BandHit.None, Edge(578, 300)); // 右帯の1px外
        Assert.Equal(BandHit.None, Edge(300, 478)); // 下帯の1px外
    }

    [Fact]
    public void EdgeCandidate_BottomRightCorner_PrefersVertical()
    {
        Assert.Equal(BandHit.Vertical, Edge(599, 499));
    }

    [Fact]
    public void EdgeCandidate_OutsideWindow_IsNone()
    {
        Assert.Equal(BandHit.None, Edge(601, 300)); // 右外
        Assert.Equal(BandHit.None, Edge(99, 300));  // 左外
        Assert.Equal(BandHit.None, Edge(300, 501)); // 下外
    }

    [Fact]
    public void EdgeCandidate_EmptyRect_IsNone()
    {
        Assert.Equal(BandHit.None, ScrollBarBand.EdgeCandidate(0, 0, 0, 0, 0, 0, 17, 17));
    }
}
