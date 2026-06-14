using Clemotius.Core.Config;

namespace Clemotius.Tests;

public class ProcessNameTests
{
    [Theory]
    [InlineData("JaneStyle", "JaneStyle")]
    [InlineData("JaneStyle.exe", "JaneStyle")]
    [InlineData("JaneStyle.EXE", "JaneStyle")]
    [InlineData("  chrome.exe  ", "chrome")]
    [InlineData("notepad", "notepad")]
    public void Normalize_TrimsAndDropsExeExtension(string input, string expected)
    {
        Assert.Equal(expected, ProcessName.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrBlank_ReturnsEmpty(string? input)
    {
        Assert.Equal("", ProcessName.Normalize(input));
    }

    [Fact]
    public void Normalize_KeepsInternalExeSubstring()
    {
        // ".exe" を含むが末尾でなければ除去しない
        Assert.Equal("my.exe.tool", ProcessName.Normalize("my.exe.tool"));
    }
}
