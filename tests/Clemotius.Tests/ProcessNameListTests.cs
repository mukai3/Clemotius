using Clemotius.Core.Config;

namespace Clemotius.Tests;

public class ProcessNameListTests
{
    [Fact]
    public void Parse_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(ProcessNameList.Parse(null));
        Assert.Empty(ProcessNameList.Parse("   "));
        Assert.Empty(ProcessNameList.Parse(", ,"));
    }

    [Fact]
    public void Parse_TrimsExeAndWhitespace()
    {
        Assert.Equal(new[] { "chrome", "msedge" }, ProcessNameList.Parse(" chrome.exe , msedge "));
    }

    [Fact]
    public void Parse_RemovesCaseInsensitiveDuplicates_PreservingOrder()
    {
        Assert.Equal(new[] { "Chrome", "edge" }, ProcessNameList.Parse("Chrome, chrome.exe, edge, CHROME"));
    }

    [Fact]
    public void Format_DedupesAndJoins()
    {
        Assert.Equal("chrome, msedge", ProcessNameList.Format(new[] { "chrome.exe", "msedge", "Chrome" }));
    }

    [Fact]
    public void Merge_AppendsNewKeepsExistingFirst()
    {
        Assert.Equal("chrome, msedge, brave", ProcessNameList.Merge("chrome, msedge", new[] { "msedge.exe", "brave" }));
    }

    [Fact]
    public void Merge_FromEmptyExisting()
    {
        Assert.Equal("brave", ProcessNameList.Merge("", new[] { "brave.exe" }));
    }
}
