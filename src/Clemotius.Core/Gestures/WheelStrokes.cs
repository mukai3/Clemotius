namespace Clemotius.Core.Gestures;

/// <summary>
/// 右ボタン+ホイールを表すストローク予約トークン。方向（U/D/L/R）に "W" は無いため衝突しない。
/// ストローク列が <see cref="Up"/>/<see cref="Down"/> の binding は右ボタン+ホイール上/下を意味する
/// （実行時のストローク認識は U/D/L/R しか生成しないため、これらが軌跡として誤マッチすることはない）。
/// </summary>
public static class WheelStrokes
{
    public const string Up = "WU";
    public const string Down = "WD";

    /// <summary>WU/WD（ホイールトリガ）かどうか。</summary>
    public static bool IsWheel(string strokes) => strokes is Up or Down;
}
