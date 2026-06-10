using Clemoutis.Core.Actions;

namespace Clemoutis.Tests;

public class KeyStrokeParserTests
{
    [Fact]
    public void Parse_CtrlW()
    {
        var s = KeyStrokeParser.Parse("Ctrl+W");
        Assert.Equal((ushort)'W', s.VirtualKey);
        Assert.True(s.Ctrl);
        Assert.False(s.Shift);
        Assert.False(s.Alt);
        Assert.False(s.Win);
    }

    [Fact]
    public void Parse_CtrlShiftTab()
    {
        var s = KeyStrokeParser.Parse("Ctrl+Shift+Tab");
        Assert.Equal((ushort)0x09, s.VirtualKey);
        Assert.True(s.Ctrl);
        Assert.True(s.Shift);
    }

    [Fact]
    public void Parse_IsOrderInsensitive()
    {
        var a = KeyStrokeParser.Parse("Ctrl+Shift+F5");
        var b = KeyStrokeParser.Parse("Shift+Ctrl+F5");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        var s = KeyStrokeParser.Parse("ctrl+home");
        Assert.Equal((ushort)0x24, s.VirtualKey);
        Assert.True(s.Ctrl);
    }

    [Theory]
    [InlineData("F5", 0x74)]
    [InlineData("End", 0x23)]
    [InlineData("Home", 0x24)]
    [InlineData("Tab", 0x09)]
    public void Parse_NamedKeys(string text, int expectedVk)
    {
        Assert.Equal((ushort)expectedVk, KeyStrokeParser.Parse(text).VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]            // 修飾のみ
    [InlineData("Ctrl+Shift")]      // 修飾のみ
    [InlineData("Ctrl+Nope")]       // 未知キー
    [InlineData("Ctrl+A+B")]        // 主キー複数
    public void TryParse_InvalidInputs_ReturnFalse(string text)
    {
        Assert.False(KeyStrokeParser.TryParse(text, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void RoundTrip_ToStringMatchesParse()
    {
        var s = KeyStrokeParser.Parse("Ctrl+Shift+Tab");
        Assert.Equal("Ctrl+Shift+Tab", s.ToString());
    }
}
