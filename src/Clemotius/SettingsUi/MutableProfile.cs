using Clemotius.Core.Actions;
using Clemotius.Core.Config;
using Clemotius.Core.Gestures;

namespace Clemotius.SettingsUi;

/// <summary>設定画面で編集するためのプロファイルの可変表現。</summary>
internal sealed class MutableProfile
{
    public string Name { get; set; } = "Default";
    public string ProcessPattern { get; set; } = "";
    public bool GesturesEnabled { get; set; } = true;

    /// <summary>ストロークと右ボタン+ホイール上/下を区別なく保持する（Trigger で種別を持つ）。</summary>
    public List<GestureBinding> Gestures { get; } = new();

    public override string ToString() => $"{Name} ({ProcessPattern})";

    public static MutableProfile From(GestureProfile p)
    {
        var m = new MutableProfile
        {
            Name = p.Name,
            ProcessPattern = p.ProcessPattern,
            GesturesEnabled = p.GesturesEnabled,
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
    };
}
