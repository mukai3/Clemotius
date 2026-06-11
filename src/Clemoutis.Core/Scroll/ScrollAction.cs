namespace Clemoutis.Core.Scroll;

/// <summary>スクロールの向き。</summary>
public enum ScrollOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>スクロールの単位。</summary>
public enum ScrollUnit
{
    Line,  // 行/列単位（Amount 回送る）
    Page,  // ページ単位
    Edge,  // 先頭/末尾（上端下端・左右端）
}

/// <summary>
/// スクロールバー操作1回ぶんの内容。WM_HSCROLL/WM_VSCROLL を Amount 回送るための情報。
/// </summary>
public sealed record ScrollAction(ScrollOrientation Orientation, ScrollUnit Unit, int Amount);
