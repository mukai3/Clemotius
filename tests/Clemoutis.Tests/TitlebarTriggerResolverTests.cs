using Clemoutis.Core.Config;
using Clemoutis.Core.Windowing;

namespace Clemoutis.Tests;

public class TitlebarTriggerResolverTests
{
    private static readonly TitlebarSettings Settings = new()
    {
        ShiftClick = "alwaysOnTop",
        CtrlClick = "windowShade",
        RightClick = "openExeFolder",
        MiddleClick = "translucent",
        MinButtonRightClick = "alwaysOnTop",
        CloseButtonRightClick = "translucent",
    };

    [Fact]
    public void CaptionTriggers_MapToConfiguredSlots()
    {
        Assert.Equal(WindowAction.AlwaysOnTop,
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Left, shift: true, ctrl: false, TitlebarHitArea.Caption));
        Assert.Equal(WindowAction.WindowShade,
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Left, shift: false, ctrl: true, TitlebarHitArea.Caption));
        Assert.Equal(WindowAction.OpenExeFolder,
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Right, shift: false, ctrl: false, TitlebarHitArea.Caption));
        Assert.Equal(WindowAction.Translucent,
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Middle, shift: false, ctrl: false, TitlebarHitArea.Caption));
    }

    [Fact]
    public void ButtonAreas_RespondToRightClickOnly()
    {
        Assert.Equal(WindowAction.AlwaysOnTop,
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Right, false, false, TitlebarHitArea.MinimizeButton));
        Assert.Equal(WindowAction.Translucent,
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Right, false, false, TitlebarHitArea.CloseButton));
        Assert.Null(
            TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Left, false, false, TitlebarHitArea.MinimizeButton));
    }

    [Fact]
    public void PlainLeftClickOnCaption_IsNotATrigger()
    {
        Assert.Null(TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Left, false, false, TitlebarHitArea.Caption));
    }

    [Fact]
    public void CtrlShiftLeftClick_IsNotATrigger()
    {
        // Shift/Ctrl 同時押しはどちらのスロットでもない
        Assert.Null(TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Left, true, true, TitlebarHitArea.Caption));
    }

    [Fact]
    public void NoneArea_ReturnsNull()
    {
        Assert.Null(TitlebarTriggerResolver.Resolve(Settings, TitlebarButton.Right, false, false, TitlebarHitArea.None));
    }

    [Fact]
    public void NoneSlot_ReturnsNull()
    {
        var defaults = new TitlebarSettings(); // 全スロット none
        Assert.Null(TitlebarTriggerResolver.Resolve(defaults, TitlebarButton.Right, false, false, TitlebarHitArea.Caption));
    }

    [Theory]
    [InlineData("alwaysOnTop", WindowAction.AlwaysOnTop)]
    [InlineData("WINDOWSHADE", WindowAction.WindowShade)]
    [InlineData("openExeFolder", WindowAction.OpenExeFolder)]
    [InlineData("translucent", WindowAction.Translucent)]
    public void Parser_AcceptsKnownValues(string value, WindowAction expected)
    {
        Assert.Equal(expected, WindowActionParser.Parse(value));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unknown")]
    public void Parser_ReturnsNullForNoneOrUnknown(string? value)
    {
        Assert.Null(WindowActionParser.Parse(value));
    }

    // ── MayMatch（ヒットテスト前の事前判定） ──

    [Fact]
    public void MayMatch_AllSlotsNone_IsFalseForEveryButton()
    {
        var defaults = new TitlebarSettings(); // 全スロット none
        Assert.False(TitlebarTriggerResolver.MayMatch(defaults, TitlebarButton.Left, shift: true, ctrl: false));
        Assert.False(TitlebarTriggerResolver.MayMatch(defaults, TitlebarButton.Right, shift: false, ctrl: false));
        Assert.False(TitlebarTriggerResolver.MayMatch(defaults, TitlebarButton.Middle, shift: false, ctrl: false));
    }

    [Fact]
    public void MayMatch_Left_RequiresMatchingModifier()
    {
        var s = new TitlebarSettings { CtrlClick = "openExeFolder" };
        Assert.True(TitlebarTriggerResolver.MayMatch(s, TitlebarButton.Left, shift: false, ctrl: true));
        // 修飾キーなし／不一致の左クリックはヒットテスト不要
        Assert.False(TitlebarTriggerResolver.MayMatch(s, TitlebarButton.Left, shift: false, ctrl: false));
        Assert.False(TitlebarTriggerResolver.MayMatch(s, TitlebarButton.Left, shift: true, ctrl: false));
        // Shift+Ctrl 同時押しは対象外（Resolve と同じ規則）
        Assert.False(TitlebarTriggerResolver.MayMatch(s, TitlebarButton.Left, shift: true, ctrl: true));
    }

    [Fact]
    public void MayMatch_Right_TrueIfAnyRightSlotConfigured()
    {
        Assert.True(TitlebarTriggerResolver.MayMatch(
            new TitlebarSettings { MinButtonRightClick = "windowShade" }, TitlebarButton.Right, false, false));
        Assert.True(TitlebarTriggerResolver.MayMatch(
            new TitlebarSettings { RightClick = "alwaysOnTop" }, TitlebarButton.Right, false, false));
        // 右系スロットが全て none なら、左用スロットが設定されていても右はヒットテスト不要
        Assert.False(TitlebarTriggerResolver.MayMatch(
            new TitlebarSettings { CtrlClick = "openExeFolder" }, TitlebarButton.Right, false, false));
    }

    [Fact]
    public void MayMatch_Middle_FollowsMiddleSlot()
    {
        Assert.True(TitlebarTriggerResolver.MayMatch(
            new TitlebarSettings { MiddleClick = "translucent" }, TitlebarButton.Middle, false, false));
        Assert.False(TitlebarTriggerResolver.MayMatch(
            new TitlebarSettings { RightClick = "alwaysOnTop" }, TitlebarButton.Middle, false, false));
    }

    [Fact]
    public void TitlebarSettings_RoundTripsThroughJson()
    {
        var config = new Clemoutis.Core.Config.ClemoutisConfig
        {
            Titlebar = Settings with { WindowOpacity = 70 },
        };
        var restored = Clemoutis.Core.Config.Json.ConfigSerializer.Deserialize(
            Clemoutis.Core.Config.Json.ConfigSerializer.Serialize(config));
        Assert.Equal(config.Titlebar, restored.Titlebar);
    }
}
