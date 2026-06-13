using Clemotius.Core.Gestures;

namespace Clemotius.Tests;

public class StrokeEncoderTests
{
    private static StrokeEncoder New() => new(range: 10);

    [Fact]
    public void NoMovement_ProducesNoStrokes()
    {
        var e = New();
        e.Begin(100, 100);
        Assert.False(e.Add(105, 103)); // しきい値未満
        Assert.False(e.HasStrokes);
        Assert.Equal("", e.ToStrokeString());
    }

    [Fact]
    public void RightMovement_ProducesRightStroke()
    {
        var e = New();
        e.Begin(100, 100);
        Assert.True(e.Add(130, 100));
        Assert.Equal("R", e.ToStrokeString());
    }

    [Theory]
    [InlineData(100, 0, "R")]
    [InlineData(-100, 0, "L")]
    [InlineData(0, 100, "D")]
    [InlineData(0, -100, "U")]
    public void SingleAxisMovement_ProducesExpectedDirection(int dx, int dy, string expected)
    {
        var e = New();
        e.Begin(200, 200);
        e.Add(200 + dx, 200 + dy);
        Assert.Equal(expected, e.ToStrokeString());
    }

    [Fact]
    public void SameDirectionRepeated_CollapsesToOneStroke()
    {
        var e = New();
        e.Begin(0, 0);
        e.Add(30, 0);
        e.Add(60, 0);
        e.Add(90, 0);
        Assert.Equal("R", e.ToStrokeString());
    }

    [Fact]
    public void DownThenRight_ProducesDR()
    {
        var e = New();
        e.Begin(0, 0);
        e.Add(0, 40);  // 下
        e.Add(40, 40); // 右（基準点が進んでいるので相対で右と判定）
        Assert.Equal("DR", e.ToStrokeString());
    }

    [Fact]
    public void DominantAxisDecidesDirection_DiagonalLeansToLargerComponent()
    {
        var e = New();
        e.Begin(0, 0);
        e.Add(50, 20); // x が支配的 → 右
        Assert.Equal("R", e.ToStrokeString());
    }

    [Fact]
    public void Begin_ClearsPreviousStrokes()
    {
        var e = New();
        e.Begin(0, 0);
        e.Add(50, 0);
        Assert.True(e.HasStrokes);
        e.Begin(0, 0);
        Assert.False(e.HasStrokes);
    }

    [Fact]
    public void Add_WithoutBegin_ReturnsFalse()
    {
        var e = New();
        Assert.False(e.Add(100, 100));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrokeEncoder(0));
    }
}
