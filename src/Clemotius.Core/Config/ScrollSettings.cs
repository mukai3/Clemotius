namespace Clemotius.Core.Config;

/// <summary>スクロール拡張の設定。既定値はユーザーの Kazaguru.ini 由来。</summary>
public sealed record ScrollSettings
{
    public int Sensitivity { get; init; } = 3;
    public int Acceleration { get; init; } = 3;
    public bool AcceleratedScroll { get; init; }
    public bool ScrollAlways { get; init; }

    /// <summary>
    /// スクロールバー上でホイールを回したときの挙動。オリジナルの「ホイール」タブの
    /// 「垂直/水平スクロールバー上でホイール回転」に対応。値は behavior 文字列
    /// （"none" / "horizontal" / 後方互換の "code:NN"）。
    /// 既定はユーザー ini を解読した結果に基づく:
    ///   垂直バー=9行スクロール(53)→垂直方向＝通常スクロール扱い "none"、
    ///   水平バー=水平6列スクロール(58)→水平化 "horizontal"。
    /// </summary>
    public string OnVerticalScrollbar { get; init; } = "code:53";   // 9 行スクロール（垂直＝通常）
    public string OnHorizontalScrollbar { get; init; } = "code:58"; // 水平 6 列スクロール（＝水平化）

    public int MergeWheelDelta { get; init; } = 2;
    public int WheelResolution { get; init; } = 1;
    public int AutoWheelResolution { get; init; } = 3;

    /// <summary>
    /// 修飾キー押下中のホイール挙動。オリジナル v1.67 と同じく6通り
    /// （Shift / Ctrl / Ctrl+Shift / Alt / Shift+Alt / Ctrl+Alt）を持つ。
    /// 値は behavior 文字列（"none" / "horizontal" / 未確定の "code:NN"）。
    /// 既定はユーザー ini 由来（Alt のみ ScrollExAlt=55、他は 0）。
    /// </summary>
    public ModifierScrollSettings ModifierScroll { get; init; } = new();
}

/// <summary>
/// 修飾キー組み合わせ別のホイール挙動。オリジナルの6スロットに対応する。
/// 既定値はユーザーの Kazaguru.ini の ScrollEx* 由来。
/// </summary>
public sealed record ModifierScrollSettings
{
    public string Shift { get; init; } = "none";        // ScrollExShift=0
    public string Ctrl { get; init; } = "none";         // ScrollExCtrl=0
    public string CtrlShift { get; init; } = "none";    // ScrollExCtrlShift=0
    public string Alt { get; init; } = "code:55";       // ScrollExAlt=55（意味未確定）
    public string ShiftAlt { get; init; } = "none";     // ScrollExShiftAlt=0
    public string CtrlAlt { get; init; } = "none";      // ScrollExCtrlAlt=0
}
