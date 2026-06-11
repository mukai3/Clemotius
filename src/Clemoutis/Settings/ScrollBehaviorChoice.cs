namespace Clemoutis.Settings;

/// <summary>
/// スクロール挙動の選択肢カタログ。動的解析で確定したコード値（垂直系=50..57 /
/// 水平系=56..63）に対応する。値は "none" または "code:NN"。
/// Clemoutis が実際に変換するのは方向（水平化/通常スクロール）のみで、量・単位
/// （行数/列数/ページ等）は値として保持するが現状の動作には未反映。
/// </summary>
internal static class ScrollBehaviorChoice
{
    internal sealed record Choice(string Display, string Value);

    public static IReadOnlyList<Choice> VerticalBar { get; } = new[]
    {
        new Choice("なし", "none"),
        new Choice("1 行スクロール", "code:50"),
        new Choice("3 行スクロール", "code:51"),
        new Choice("6 行スクロール", "code:52"),
        new Choice("9 行スクロール", "code:53"),
        new Choice("12 行スクロール", "code:54"),
        new Choice("ページ スクロール", "code:55"),
        new Choice("高速スクロール", "code:56"),
        new Choice("上端/下端にスクロール", "code:57"),
    };

    public static IReadOnlyList<Choice> HorizontalBar { get; } = new[]
    {
        new Choice("なし", "none"),
        new Choice("水平 1 列スクロール", "code:56"),
        new Choice("水平 3 列スクロール", "code:57"),
        new Choice("水平 6 列スクロール", "code:58"),
        new Choice("水平 9 列スクロール", "code:59"),
        new Choice("水平 12 列スクロール", "code:60"),
        new Choice("水平ページスクロール", "code:61"),
        new Choice("水平高速スクロール", "code:62"),
        new Choice("左端/右端にスクロール", "code:63"),
    };

    /// <summary>修飾キー＋ホイール用: なし＋垂直系＋水平系。</summary>
    public static IReadOnlyList<Choice> Modifier { get; } =
        new[] { new Choice("なし", "none") }
            .Concat(VerticalBar.Skip(1))
            .Concat(HorizontalBar.Skip(1))
            .ToList();

    /// <summary>各コンボ用に独立した DataSource リストを返す（共有不可のため）。</summary>
    public static List<Choice> CopyOf(IReadOnlyList<Choice> source) => source.ToList();
}
