namespace Clemoutis.Core.Scroll;

/// <summary>
/// 設定 behavior 文字列を <see cref="WheelConversion"/> に変換する。
/// 既知のキーワードのみ解釈し、未確定のコード値（"code:55" 等）は None に倒す。
/// それらコードの意味は動的解析（設計書 D.4）で確定後にここへ追加する。
/// </summary>
public static class ScrollBehaviorParser
{
    // オリジナルのスクロール動作コードの方向ビット。
    // 動的解析（appdefault→modscroll の差分）で確定: 57(水平3列)/58(水平既定)は水平、
    // 50(垂直1行)/53(垂直既定)は垂直。全サンプルで bit3(0x08)=方向(1=水平/0=垂直) が整合。
    // 量・単位（行/列/ページ数）の完全な意味は未確定だが、方向だけは判別できる。
    private const int HorizontalDirectionBit = 0x08;

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

        // "code:NN"（オリジナルのコード値）は方向ビットで水平/垂直を判定する。
        // 垂直は通常スクロールと同義なので素通し（None）扱い。
        if (s.StartsWith("code:", StringComparison.Ordinal)
            && int.TryParse(s.AsSpan("code:".Length), out int code))
        {
            return (code & HorizontalDirectionBit) != 0
                ? WheelConversion.Horizontal
                : WheelConversion.None;
        }

        return WheelConversion.None;
    }
}
