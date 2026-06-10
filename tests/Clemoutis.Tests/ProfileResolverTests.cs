using Clemoutis.Core.Config;

namespace Clemoutis.Tests;

public class ProfileResolverTests
{
    private static GestureProfile P(string name, string pattern) =>
        new() { Name = name, ProcessPattern = pattern };

    [Fact]
    public void Resolve_SpecificProcess_BeatsWildcard()
    {
        var r = new ProfileResolver(new[] { P("Default", "*"), P("Chrome", "chrome") });
        Assert.Equal("Chrome", r.Resolve("chrome")!.Name);
    }

    [Fact]
    public void Resolve_IgnoresExeExtension()
    {
        var r = new ProfileResolver(new[] { P("Default", "*"), P("Chrome", "chrome.exe") });
        Assert.Equal("Chrome", r.Resolve("chrome")!.Name);
        Assert.Equal("Chrome", r.Resolve("chrome.exe")!.Name);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var r = new ProfileResolver(new[] { P("Chrome", "Chrome") });
        Assert.Equal("Chrome", r.Resolve("CHROME")!.Name);
    }

    [Fact]
    public void Resolve_NoMatch_FallsBackToWildcard()
    {
        var r = new ProfileResolver(new[] { P("Default", "*"), P("Chrome", "chrome") });
        Assert.Equal("Default", r.Resolve("notepad")!.Name);
    }

    [Fact]
    public void Resolve_NoWildcardAndNoMatch_ReturnsNull()
    {
        var r = new ProfileResolver(new[] { P("Chrome", "chrome") });
        Assert.Null(r.Resolve("notepad"));
    }

    [Fact]
    public void Resolve_FirstSpecificMatchWins()
    {
        var r = new ProfileResolver(new[] { P("A", "chrome"), P("B", "chrome") });
        Assert.Equal("A", r.Resolve("chrome")!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyProcess_FallsBackToWildcard(string? process)
    {
        var r = new ProfileResolver(new[] { P("Default", "*"), P("Chrome", "chrome") });
        Assert.Equal("Default", r.Resolve(process)!.Name);
    }
}
