using Clemotius.Core.Actions;
using Clemotius.Core.Config;
using Clemotius.Core.Config.Json;
using Clemotius.Core.Gestures;

namespace Clemotius.Tests;

public class ConfigSerializerTests
{
    [Fact]
    public void DefaultConfig_RoundTrips()
    {
        // レコードのコレクションプロパティは参照等価のため、再シリアライズして比較する
        var original = ClemotiusConfig.CreateDefault();
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
    public void KeyActionWithLabel_RoundTrips()
    {
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("DR", new KeyAction(KeyStrokeParser.Parse("Ctrl+T"), "Chrome: 新規タブを開く (Ctrl+T)"))));
        var key = Assert.IsType<KeyAction>(FirstAction(ConfigSerializer.Deserialize(json)));
        Assert.Equal("Chrome: 新規タブを開く (Ctrl+T)", key.Label);
        Assert.Equal("Ctrl+T", key.Stroke.ToString());
    }

    [Fact]
    public void KeyActionWithoutLabel_OmitsLabelInJson()
    {
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("DR", new KeyAction(KeyStrokeParser.Parse("Ctrl+W")))));
        Assert.DoesNotContain("\"label\"", json);
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
        var c = ClemotiusConfig.CreateDefault();
        Assert.Equal(8, c.Gesture.Range);
        Assert.Equal(1000, c.Gesture.TimeoutMs);
        Assert.False(c.Gesture.DrawStroke);
        Assert.Equal("#80FF00", c.Gesture.ValidStrokeColor);
        Assert.Equal(3, c.Scroll.Sensitivity);
    }

    [Fact]
    public void DefaultProfile_HasWheelTabSwitchBindings()
    {
        // R+WU=前のタブ / R+WD=次のタブ（ユーザー ini 由来）
        var profile = ClemotiusConfig.DefaultProfile();
        var up = Assert.IsType<KeyAction>(profile.WheelUp);
        var down = Assert.IsType<KeyAction>(profile.WheelDown);
        Assert.Equal("Ctrl+Shift+Tab", up.Stroke.ToString());
        Assert.Equal("Ctrl+Tab", down.Stroke.ToString());
    }

    [Fact]
    public void WheelActions_RoundTrip()
    {
        var original = ClemotiusConfig.CreateDefault();
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(original));
        var up = Assert.IsType<KeyAction>(restored.Profiles[0].WheelUp);
        Assert.Equal("Ctrl+Shift+Tab", up.Stroke.ToString());
    }

    [Fact]
    public void NullWheelActions_RoundTrip()
    {
        var config = new ClemotiusConfig
        {
            Profiles = new[] { new GestureProfile { Name = "T", ProcessPattern = "*" } },
        };
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));
        Assert.Null(restored.Profiles[0].WheelUp);
        Assert.Null(restored.Profiles[0].WheelDown);
    }

    private static ClemotiusConfig WithGesture(GestureBinding binding) => new()
    {
        Profiles = new[]
        {
            new GestureProfile { Name = "T", ProcessPattern = "*", Gestures = new[] { binding } },
        },
    };

    private static GestureAction FirstAction(ClemotiusConfig c) =>
        c.Profiles[0].Gestures[0].Action;
}
