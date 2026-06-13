using Clemotius.Core.Actions;
using Clemotius.Core.Config;
using Clemotius.Core.Gestures;

namespace Clemotius.Tests;

public class ProfileResolverTests
{
    private static GestureProfile P(string name, string pattern) =>
        new() { Name = name, ProcessPattern = pattern };

    private static GestureBinding Key(string strokes, string keys) =>
        new(strokes, new KeyAction(KeyStrokeParser.Parse(keys)));

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
    [InlineData("chrome")]
    [InlineData("edge")]
    [InlineData("brave.exe")]
    public void Resolve_CommaSeparatedPatterns_MatchAny(string process)
    {
        var r = new ProfileResolver(new[]
        {
            P("Default", "*"),
            P("Browsers", "chrome.exe, edge.exe, brave.exe"),
        });
        Assert.Equal("Browsers", r.Resolve(process)!.Name);
    }

    [Fact]
    public void Resolve_CommaSeparated_NoMatch_FallsBackToWildcard()
    {
        var r = new ProfileResolver(new[]
        {
            P("Default", "*"),
            P("Browsers", "chrome, edge, brave"),
        });
        Assert.Equal("Default", r.Resolve("notepad")!.Name);
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

    // ── グローバル(*)とアプリ別のマージ（ResolveEffective） ──

    private static ProfileResolver MergeSetup()
    {
        var global = new GestureProfile
        {
            Name = "Default", ProcessPattern = "*",
            Gestures = new[] { Key("L", "Alt+Left"), Key("R", "Alt+Right") },
            WheelUp = new KeyAction(KeyStrokeParser.Parse("Ctrl+Shift+Tab")),
        };
        var chrome = new GestureProfile
        {
            Name = "Chrome", ProcessPattern = "chrome",
            Gestures = new[] { Key("DR", "Ctrl+W"), Key("L", "Ctrl+P") }, // L はグローバルを上書き
        };
        return new ProfileResolver(new[] { global, chrome });
    }

    [Fact]
    public void ResolveEffective_MergesGlobalAndSpecific()
    {
        var eff = MergeSetup().ResolveEffective("chrome")!;
        var strokes = eff.Gestures.Select(g => g.Strokes).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "DR", "L", "R" }, strokes); // グローバルのR + 上書きL + 追加DR
    }

    [Fact]
    public void ResolveEffective_SpecificOverridesGlobalStroke()
    {
        var eff = MergeSetup().ResolveEffective("chrome")!;
        var l = (KeyAction)eff.Gestures.First(g => g.Strokes == "L").Action;
        Assert.Equal("Ctrl+P", l.Stroke.ToString()); // グローバルの Alt+Left でなく上書き
    }

    [Fact]
    public void ResolveEffective_InheritsWheelFromGlobalWhenSpecificHasNone()
    {
        var eff = MergeSetup().ResolveEffective("chrome")!;
        Assert.NotNull(eff.WheelUp); // chrome は未設定だがグローバルから継承
    }

    [Fact]
    public void ResolveEffective_NoSpecific_ReturnsGlobal()
    {
        var eff = MergeSetup().ResolveEffective("notepad")!;
        Assert.Equal("Default", eff.Name);
        Assert.Equal(2, eff.Gestures.Count);
    }

    [Fact]
    public void ResolveEffective_NoGlobal_ReturnsSpecificOnly()
    {
        var r = new ProfileResolver(new[]
        {
            new GestureProfile { Name = "Chrome", ProcessPattern = "chrome",
                Gestures = new[] { Key("DR", "Ctrl+W") } },
        });
        Assert.Equal("Chrome", r.ResolveEffective("chrome")!.Name);
    }
}
