using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.Core.Config;

/// <summary>
/// アプリ単位のプロファイル。前面プロセス名/クラス名のパターンで適用先を決める。
/// </summary>
public sealed record GestureProfile
{
    public string Name { get; init; } = "Default";

    /// <summary>適用対象。プロセス名（拡張子なし可）またはワイルドカード "*"。</summary>
    public string ProcessPattern { get; init; } = "*";

    public bool GesturesEnabled { get; init; } = true;

    public IReadOnlyList<GestureBinding> Gestures { get; init; } = Array.Empty<GestureBinding>();

    /// <summary>右ボタン押下中にホイールを上回転したときのアクション。null なら無し。</summary>
    public GestureAction? WheelUp { get; init; }

    /// <summary>右ボタン押下中にホイールを下回転したときのアクション。null なら無し。</summary>
    public GestureAction? WheelDown { get; init; }
}
