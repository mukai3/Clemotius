namespace Clematius.Core.Config;

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
    /// （"none" / "horizontal" / "code:NN"）。
    /// 既定: 垂直バー=ページスクロール(55)、水平バー=水平3列スクロール(57)。
    /// </summary>
    public string OnVerticalScrollbar { get; init; } = "code:55";   // ページスクロール
    public string OnHorizontalScrollbar { get; init; } = "code:57"; // 水平 3 列スクロール

    /// <summary>
    /// MSAA カスタム描画スクロールバー（Excel 等）をスクロールバーとして検出するか。
    /// 既定 false。標準バー（独立 ScrollBar コントロール／非クライアントの WS_*SCROLL）は
    /// この設定に関係なく常に検出する。有効時はマウス移動毎・ホイール毎にクロスプロセスの
    /// MSAA 検出（バックグラウンド先読み）が走るため負荷が増える。
    /// </summary>
    public bool DetectOfficeScrollbar { get; init; }

    /// <summary>
    /// UIA カスタム描画スクロールバー（Chromium 系ブラウザの横バー等）を検出するか。
    /// 既定 false。有効時はブラウザ描画窓上でカーソル静止時に UIA 検出（先読み）が走る。
    /// 無効時はブラウザ横スクロールを修飾キー＋ホイール（<see cref="ModifierScroll"/>）で代替する。
    /// </summary>
    public bool DetectBrowserScrollbar { get; init; }

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
