using Clematius.Core.Actions;
using Clematius.Core.Config;
using Clematius.Core.Config.Json;
using Clematius.Core.Gestures;

namespace Clematius.Tests;

public class ConfigSerializerTests
{
    [Fact]
    public void DefaultConfig_RoundTrips()
    {
        // レコードのコレクションプロパティは参照等価のため、再シリアライズして比較する
        var original = ClematiusConfig.CreateDefault();
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
    public void LegacyCloseCommand_MapsToBrowserClose()
    {
        // 旧名 "Close" で保存された appcommand は BrowserClose として読める（コマンド名変更の互換）
        var json = ConfigSerializer.Serialize(WithGesture(
            new GestureBinding("DR", new AppCommandAction(AppCommand.BrowserClose))))
            .Replace("BrowserClose", "Close");
        var cmd = Assert.IsType<AppCommandAction>(FirstAction(ConfigSerializer.Deserialize(json)));
        Assert.Equal(AppCommand.BrowserClose, cmd.Command);
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
        var c = ClematiusConfig.CreateDefault();
        Assert.Equal(8, c.Gesture.Range);
        Assert.Equal(1000, c.Gesture.TimeoutMs);
        Assert.False(c.Gesture.DrawStroke);
        Assert.Equal("#80FF00", c.Gesture.ValidStrokeColor);
        Assert.Equal(3, c.Scroll.Sensitivity);
    }

    [Fact]
    public void DefaultProfiles_IsBrowserOnly()
    {
        // グローバル("*")は廃止。既定はブラウザ用プロファイル1件のみ。
        var c = ClematiusConfig.CreateDefault();
        var profile = Assert.Single(c.Profiles);
        Assert.Equal("ブラウザ", profile.Name);
        Assert.Equal("chrome, msedge", profile.ProcessPattern);
        Assert.DoesNotContain(c.Profiles, p => p.ProcessPattern.Trim() == "*");
    }

    [Fact]
    public void DefaultBrowserProfile_MatchesSpec()
    {
        var profile = ClematiusConfig.DefaultBrowserProfile();
        Assert.Equal("chrome, msedge", profile.ProcessPattern);

        // ストローク構成（既定値の参照仕様）。右ボタン+ホイール(WU/WD)は除く。
        var strokes = profile.Gestures
            .Where(g => !WheelStrokes.IsWheel(g.Strokes))
            .Select(g => g.Strokes).ToArray();
        Assert.Equal(new[] { "L", "R", "RU", "RD", "DR", "LR", "DL", "UDUD", "UD" }, strokes);

        GestureAction ActionOf(string s) => profile.Gestures.First(g => g.Strokes == s).Action;
        Assert.Equal(AppCommand.BrowserBackward, Assert.IsType<AppCommandAction>(ActionOf("L")).Command);
        Assert.Equal(AppCommand.BrowserForward, Assert.IsType<AppCommandAction>(ActionOf("R")).Command);
        Assert.Equal("Ctrl+Home", Assert.IsType<KeyAction>(ActionOf("RU")).Stroke.ToString());
        Assert.Equal("Ctrl+End", Assert.IsType<KeyAction>(ActionOf("RD")).Stroke.ToString());
        Assert.Equal(AppCommand.BrowserClose, Assert.IsType<AppCommandAction>(ActionOf("DR")).Command);
        Assert.Equal("Ctrl+T", Assert.IsType<KeyAction>(ActionOf("LR")).Stroke.ToString());
        Assert.Equal("Ctrl+Shift+T", Assert.IsType<KeyAction>(ActionOf("DL")).Stroke.ToString());
        Assert.Equal("Ctrl+F5", Assert.IsType<KeyAction>(ActionOf("UDUD")).Stroke.ToString());
        Assert.Equal(AppCommand.BrowserRefresh, Assert.IsType<AppCommandAction>(ActionOf("UD")).Command);

        // 右ボタン+ホイール（タブ切替）
        Assert.Equal("Ctrl+Shift+Tab", Assert.IsType<KeyAction>(profile.WheelUp).Stroke.ToString());
        Assert.Equal("Ctrl+Tab", Assert.IsType<KeyAction>(profile.WheelDown).Stroke.ToString());
    }

    [Fact]
    public void DefaultScrollbarBehavior_IsPageAndThreeColumns()
    {
        var s = ClematiusConfig.CreateDefault().Scroll;
        Assert.Equal("code:55", s.OnVerticalScrollbar);   // 垂直バー＝ページスクロール
        Assert.Equal("code:57", s.OnHorizontalScrollbar); // 水平バー＝水平3列スクロール
    }

    [Fact]
    public void ScrollbarDetectionFlags_DefaultOff()
    {
        var s = ClematiusConfig.CreateDefault().Scroll;
        Assert.False(s.DetectOfficeScrollbar);
        Assert.False(s.DetectBrowserScrollbar);
    }

    [Fact]
    public void ScrollbarDetectionFlags_RoundTrip()
    {
        var original = new ClematiusConfig
        {
            Scroll = new ScrollSettings { DetectOfficeScrollbar = true, DetectBrowserScrollbar = true },
        };
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(original));
        Assert.True(restored.Scroll.DetectOfficeScrollbar);
        Assert.True(restored.Scroll.DetectBrowserScrollbar);
    }

    [Fact]
    public void WheelActions_RoundTrip()
    {
        // 既定では Profiles[0] がブラウザ用（タブ切替の右+ホイールを持つ）
        var original = ClematiusConfig.CreateDefault();
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(original));
        var up = Assert.IsType<KeyAction>(restored.Profiles[0].WheelUp);
        Assert.Equal("Ctrl+Shift+Tab", up.Stroke.ToString());
    }

    [Fact]
    public void NullWheelActions_RoundTrip()
    {
        var config = new ClematiusConfig
        {
            Profiles = new[] { new GestureProfile { Name = "T", ProcessPattern = "test" } },
        };
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));
        Assert.Null(restored.Profiles[0].WheelUp);
        Assert.Null(restored.Profiles[0].WheelDown);
    }

    [Fact]
    public void WheelBinding_RoundTrips()
    {
        // 右ボタン+ホイールは一覧の binding（ストローク WU/WD）として保持・往復する。
        var config = WithGesture(new GestureBinding(WheelStrokes.Down, new KeyAction(KeyStrokeParser.Parse("Ctrl+Tab"))));
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));
        var b = Assert.Single(restored.Profiles[0].Gestures);
        Assert.Equal("WD", b.Strokes);
        Assert.Equal("Ctrl+Tab", Assert.IsType<KeyAction>(restored.Profiles[0].WheelDown).Stroke.ToString());
    }

    [Fact]
    public void LegacyWheelFields_MigrateToBindings()
    {
        // 旧形式（profiles[].wheelUp / wheelDown の独立フィールド）は読込時に WU/WD binding へ移行する。
        var json = """
        {
          "profiles": [
            {
              "name": "L", "processPattern": "x", "gesturesEnabled": true, "gestures": [],
              "wheelUp": { "type": "key", "keys": "Ctrl+Shift+Tab" },
              "wheelDown": { "type": "key", "keys": "Ctrl+Tab" }
            }
          ]
        }
        """;
        var restored = ConfigSerializer.Deserialize(json);
        var p = Assert.Single(restored.Profiles);
        Assert.Equal("Ctrl+Shift+Tab", Assert.IsType<KeyAction>(p.WheelUp).Stroke.ToString());
        Assert.Equal("Ctrl+Tab", Assert.IsType<KeyAction>(p.WheelDown).Stroke.ToString());
        Assert.Equal(2, p.Gestures.Count);
        Assert.Contains(p.Gestures, b => b.Strokes == "WU");
        Assert.Contains(p.Gestures, b => b.Strokes == "WD");
    }

    [Fact]
    public void Deserialize_DropsLegacyGlobalProfile()
    {
        // 旧モデルの "*" グローバルプロファイルは読込時に除去され、補充はしない。
        var legacy = new ClematiusConfig
        {
            Profiles = new[]
            {
                new GestureProfile { Name = "Default", ProcessPattern = "*" },
                new GestureProfile { Name = "ブラウザ", ProcessPattern = "chrome" },
            },
        };
        var json = ConfigSerializer.Serialize(legacy);
        var restored = ConfigSerializer.Deserialize(json);
        var profile = Assert.Single(restored.Profiles);
        Assert.Equal("ブラウザ", profile.Name);
    }

    [Fact]
    public void Deserialize_AllGlobalProfiles_ResultsInEmpty()
    {
        // "*" のみの設定は移行後 0 件になる（補充しない）。
        var legacy = new ClematiusConfig
        {
            Profiles = new[] { new GestureProfile { Name = "Default", ProcessPattern = "*" } },
        };
        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(legacy));
        Assert.Empty(restored.Profiles);
    }

    private static ClematiusConfig WithGesture(GestureBinding binding) => new()
    {
        Profiles = new[]
        {
            new GestureProfile { Name = "T", ProcessPattern = "test", Gestures = new[] { binding } },
        },
    };

    private static GestureAction FirstAction(ClematiusConfig c) =>
        c.Profiles[0].Gestures[0].Action;
}
