using Clemotius.Core.Actions;
using Clemotius.Core.Config;
using Clemotius.Core.Gestures;

namespace Clemotius.SettingsUi;

/// <summary>設定画面で編集するためのプロファイルの可変表現。</summary>
internal sealed class MutableProfile
{
    public const string GlobalName = "グローバル";

    public string Name { get; set; } = "Default";
    public string ProcessPattern { get; set; } = "*";
    public bool GesturesEnabled { get; set; } = true;

    /// <summary>全アプリ共通のグローバルプロファイル（ProcessPattern が "*"）か。</summary>
    public bool IsGlobal => ProcessPattern == "*";
    public List<GestureBinding> Gestures { get; } = new();
    public GestureAction? WheelUp { get; set; }
    public GestureAction? WheelDown { get; set; }

    public override string ToString() => $"{Name} ({ProcessPattern})";

    public static MutableProfile From(GestureProfile p)
    {
        var m = new MutableProfile
        {
            Name = p.Name,
            ProcessPattern = p.ProcessPattern,
            GesturesEnabled = p.GesturesEnabled,
            WheelUp = p.WheelUp,
            WheelDown = p.WheelDown,
        };
        m.Gestures.AddRange(p.Gestures);
        return m;
    }

    public GestureProfile ToProfile() => new()
    {
        Name = Name,
        ProcessPattern = ProcessPattern,
        GesturesEnabled = GesturesEnabled,
        Gestures = Gestures.ToArray(),
        WheelUp = WheelUp,
        WheelDown = WheelDown,
    };
}
