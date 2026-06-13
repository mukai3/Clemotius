using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.Tests;

public class GestureMatcherTests
{
    private static GestureMatcher Build() => new(new[]
    {
        new GestureBinding("DR", new KeyAction(KeyStrokeParser.Parse("Ctrl+W"))),
        new GestureBinding("L", new AppCommandAction(AppCommand.BrowserBackward)),
        new GestureBinding("UD", new CloseAction()),
    });

    [Fact]
    public void Match_KnownStrokes_ReturnsAction()
    {
        var m = Build();
        Assert.IsType<KeyAction>(m.Match("DR"));
        Assert.IsType<AppCommandAction>(m.Match("L"));
        Assert.IsType<CloseAction>(m.Match("UD"));
    }

    [Fact]
    public void Match_UnknownStrokes_ReturnsNull()
    {
        Assert.Null(Build().Match("RD"));
    }

    [Fact]
    public void Match_IsExact_NotPrefix()
    {
        // "D" は "DR" の前方一致だが完全一致ではないので null
        Assert.Null(Build().Match("D"));
    }

    [Fact]
    public void DuplicateStrokes_LastDefinitionWins()
    {
        var m = new GestureMatcher(new[]
        {
            new GestureBinding("R", new AppCommandAction(AppCommand.BrowserForward)),
            new GestureBinding("R", new CloseAction()),
        });
        Assert.IsType<CloseAction>(m.Match("R"));
    }
}
