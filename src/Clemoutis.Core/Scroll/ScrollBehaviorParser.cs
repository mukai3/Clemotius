namespace Clemoutis.Core.Scroll;

/// <summary>
/// 設定 behavior 文字列を <see cref="WheelConversion"/> に変換する。
/// 既知のキーワードのみ解釈し、未確定のコード値（"code:55" 等）は None に倒す。
/// それらコードの意味は動的解析（設計書 D.4）で確定後にここへ追加する。
/// </summary>
public static class ScrollBehaviorParser
{
    // オリジナルのスクロール動作コードは選択肢の連番（動的解析で完全確定）:
    //   垂直系: 1行=50 3行=51 6行=52 9行=53 12行=54 ページ=55 高速=56 上端下端=57
    //   水平系: 1列=56 3列=57 6列=58 9列=59 12列=60 ページ=61 高速=62 左右端=63
    // 56/57 は垂直(高速/上端下端)と水平(1列/3列)で重複し、本来の方向は「どちらのスロット
    // (垂直バー用/水平バー用)か」で決まる。コード単体から推定する場合は値域で近似する
    // （56 以上＝水平系）。垂直の高速/上端下端(56/57)を割り当てる稀なケースのみ水平に倒れる。
    private const int HorizontalCodeThreshold = 56;

    public static WheelConversion Parse(string? behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior))
            return WheelConversion.None;

        string s = behavior.Trim().ToLowerInvariant();
        switch (s)
        {
            case "horizontal":
                return WheelConversion.Horizontal;
            case "none" or "passthrough":
                return WheelConversion.None;
        }

        // "code:NN"（オリジナルのコード値）は値域で水平/垂直を近似する。
        // 垂直系は通常スクロールと同義なので素通し（None）扱い。
        if (s.StartsWith("code:", StringComparison.Ordinal)
            && int.TryParse(s.AsSpan("code:".Length), out int code))
        {
            return code >= HorizontalCodeThreshold
                ? WheelConversion.Horizontal
                : WheelConversion.None;
        }

        return WheelConversion.None;
    }
}
