namespace Clemoutis.Core.Scroll;

/// <summary>
/// behavior 文字列（"none" / "horizontal" / "code:NN"）を <see cref="ScrollAction"/> に変換する。
/// コード値は動的解析で確定した連番:
///   垂直系 50..57 = 1行/3行/6行/9行/12行/ページ/高速/上端下端
///   水平系 56..63 = 1列/3列/6列/9列/12列/ページ/高速/左右端
/// 56/57 は垂直(高速/上端下端)と水平(1列/3列)で重複するため、適用スロットの向きで解釈する。
/// スロット不明（修飾キー）の場合は値域（56 以上＝水平）で近似する。
/// </summary>
public static class ScrollCodeDecoder
{
    private const int HighSpeedLines = 30; // 「高速スクロール」の暫定行数

    public static ScrollAction? Decode(string? behavior, ScrollOrientation? slot)
    {
        if (string.IsNullOrWhiteSpace(behavior))
            return null;

        string s = behavior.Trim().ToLowerInvariant();
        switch (s)
        {
            case "none" or "passthrough":
                return null;
            case "horizontal":
                return new ScrollAction(ScrollOrientation.Horizontal, ScrollUnit.Line, 3);
        }

        if (!s.StartsWith("code:", StringComparison.Ordinal)
            || !int.TryParse(s.AsSpan("code:".Length), out int code))
        {
            return null;
        }

        ScrollOrientation orientation;
        int index;
        if (slot == ScrollOrientation.Vertical)
        {
            orientation = ScrollOrientation.Vertical;
            index = code - 50;
        }
        else if (slot == ScrollOrientation.Horizontal)
        {
            orientation = ScrollOrientation.Horizontal;
            index = code - 56;
        }
        else if (code >= 56)
        {
            orientation = ScrollOrientation.Horizontal;
            index = code - 56;
        }
        else
        {
            orientation = ScrollOrientation.Vertical;
            index = code - 50;
        }

        // index: 0=1, 1=3, 2=6, 3=9, 4=12（行/列）, 5=ページ, 6=高速, 7=端
        return index switch
        {
            0 => new ScrollAction(orientation, ScrollUnit.Line, 1),
            1 => new ScrollAction(orientation, ScrollUnit.Line, 3),
            2 => new ScrollAction(orientation, ScrollUnit.Line, 6),
            3 => new ScrollAction(orientation, ScrollUnit.Line, 9),
            4 => new ScrollAction(orientation, ScrollUnit.Line, 12),
            5 => new ScrollAction(orientation, ScrollUnit.Page, 1),
            6 => new ScrollAction(orientation, ScrollUnit.Line, HighSpeedLines),
            7 => new ScrollAction(orientation, ScrollUnit.Edge, 1),
            _ => null,
        };
    }
}
