using Clemoutis.Core.Actions;
using Clemoutis.Core.Config;
using Clemoutis.Core.Config.Json;
using Clemoutis.Core.Gestures;

namespace Clemoutis.Tests;

public class ConfigSerializerTests
{
    [Fact]
    public void DefaultConfig_RoundTrips()
    {
        // レコードのコレクションプロパティは参照等価のため、再シリアライズして比較する
        var original = ClemoutisConfig.CreateDefault();
        var json = ConfigSerializer.Serialize(original);
        var restored = ConfigSerializer.Deserialize(json);
        Assert.Equal(json, ConfigSerializer.Serialize(restored));
    }

    [Fact]
    public void KeyAction_RoundTrips()
    {
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("DR", new KeyAction(KeyStrokeParser.Parse("Ctrl+W")))));
        var action = FirstAction(ConfigSerializer.Deserialize(json));
        var key = Assert.IsType<KeyAction>(action);
        Assert.Equal("Ctrl+W", key.Stroke.ToString());
    }

    [Fact]
    public void AppCommandAction_RoundTrips()
    {
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("L", new AppCommandAction(AppCommand.BrowserBackward))));
        var action = FirstAction(ConfigSerializer.Deserialize(json));
        var cmd = Assert.IsType<AppCommandAction>(action);
        Assert.Equal(AppCommand.BrowserBackward, cmd.Command);
    }

    [Fact]
    public void CloseAction_RoundTrips()
    {
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("UD", new CloseAction())));
        Assert.IsType<CloseAction>(FirstAction(ConfigSerializer.Deserialize(json)));
    }

    [Fact]
    public void Serialize_UsesCamelCaseAndTypeDiscriminator()
    {
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("DR", new KeyAction(KeyStrokeParser.Parse("Ctrl+W")))));
        Assert.Contains("\"strokes\"", json);
        Assert.Contains("\"type\": \"key\"", json);
        Assert.Contains("\"keys\": \"Ctrl+W\"", json);
    }

    [Fact]
    public void DefaultValues_ComeFromUserIni()
    {
        var c = ClemoutisConfig.CreateDefault();
        Assert.Equal(8, c.Gesture.Range);
        Assert.Equal(1000, c.Gesture.TimeoutMs);
        Assert.False(c.Gesture.DrawStroke);
        Assert.Equal("#80FF00", c.Gesture.ValidStrokeColor);
        Assert.Equal(3, c.Scroll.Sensitivity);
    }

    private static ClemoutisConfig WithGesture(GestureBinding binding) => new()
    {
        Profiles = new[]
        {
            new GestureProfile { Name = "T", ProcessPattern = "*", Gestures = new[] { binding } },
        },
    };

    private static GestureAction FirstAction(ClemoutisConfig c) =>
        c.Profiles[0].Gestures[0].Action;
}
