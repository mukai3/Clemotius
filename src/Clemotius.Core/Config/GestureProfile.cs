using System.Text.Json.Serialization;
using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.Core.Config;

/// <summary>
/// アプリ単位のプロファイル。前面プロセス名のパターンで適用先を決める。
/// パターンに一致するアプリでのみジェスチャーが有効になる（グローバル既定は持たない）。
/// </summary>
public sealed record GestureProfile
{
    public string Name { get; init; } = "Default";

    /// <summary>
    /// 適用対象のプロセス名（拡張子なし可、カンマ区切りで複数可）。
    /// 空ならどのアプリにも一致しない（プロファイルは事実上無効）。
    /// </summary>
    public string ProcessPattern { get; init; } = "";

    public bool GesturesEnabled { get; init; } = true;

    /// <summary>
    /// ジェスチャー定義の一覧。ストローク（U/D/L/R）と右ボタン+ホイール（WU/WD）を
    /// 区別なく保持する（ストローク列の値で種別が決まる）。
    /// </summary>
    public IReadOnlyList<GestureBinding> Gestures { get; init; } = Array.Empty<GestureBinding>();

    /// <summary>右ボタン押下中にホイールを上回転したときのアクション。null なら無し。</summary>
    /// <remarks>一覧の WU ストローク binding から導出する（先頭一致）。設定の保存先は <see cref="Gestures"/>。</remarks>
    [JsonIgnore]
    public GestureAction? WheelUp => FirstActionOf(WheelStrokes.Up);

    /// <summary>右ボタン押下中にホイールを下回転したときのアクション。null なら無し。</summary>
    [JsonIgnore]
    public GestureAction? WheelDown => FirstActionOf(WheelStrokes.Down);

    private GestureAction? FirstActionOf(string strokes)
        => Gestures.FirstOrDefault(b => b.Strokes == strokes)?.Action;
}
