using Clemoutis.Core.Config;

namespace Clemoutis.Gestures;

/// <summary>
/// ジェスチャーの軌跡を画面全体に重ねて描画する、クリック透過のトップモスト オーバーレイ。
/// マウス入力を一切受けない（WS_EX_TRANSPARENT）。TransparencyKey で背景を透明化する。
/// </summary>
internal sealed class GestureTrailOverlay : Form
{
    private static readonly Color TransparentKey = Color.Magenta;

    /// <summary>描画方法。0=軌跡(線)、1=コマンド表示(テキスト)。</summary>
    internal enum DrawMode { Trail, Command }

    private readonly List<Point> _points = new();
    private Color _strokeColor = Color.FromArgb(0x80, 0xFF, 0x00);
    private int _strokeWidth = 2;
    private DrawMode _mode = DrawMode.Trail;
    private string _commandText = "";

    public GestureTrailOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = TransparentKey;
        TransparencyKey = TransparentKey;
        StartPosition = FormStartPosition.Manual;
        Visible = false;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_TRANSPARENT = 0x20;
            const int WS_EX_TOOLWINDOW = 0x80;
            const int WS_EX_NOACTIVATE = 0x8000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void ApplySettings(GestureSettings g)
    {
        _strokeWidth = Math.Max(1, g.StrokeWidth);
        _strokeColor = ParseColor(g.ValidStrokeColor);
        _mode = g.DrawingType == 1 ? DrawMode.Command : DrawMode.Trail;
    }

    /// <summary>コマンド表示モードで表示するテキストを設定する。</summary>
    public void SetCommand(string text)
    {
        _commandText = text;
        if (Visible && _mode == DrawMode.Command)
            Invalidate();
    }

    /// <summary>ジェスチャー開始。全画面に広げて軌跡をリセットする。</summary>
    public void Begin(int screenX, int screenY)
    {
        Bounds = SystemInformation.VirtualScreen;
        _points.Clear();
        _commandText = "";
        _points.Add(ToClient(screenX, screenY));
        // 初回のみ表示し、以後は隠さない。Hide/Show を繰り返すと DWM が前回フレームを
        // 一瞬表示してしまうため（前回の軌跡が一瞬残る不具合の原因）。透過背景＋空内容は
        // 視覚的に不可視なので常時表示で問題ない。
        if (!Visible)
            Visible = true;
        Invalidate();
    }

    public void AddPoint(int screenX, int screenY)
    {
        if (!Visible)
            return;
        _points.Add(ToClient(screenX, screenY));
        Invalidate();
    }

    public void End()
    {
        // Hide() は呼ばない。内容をクリアして再描画するだけ（透過背景＋空内容は不可視）。
        // Hide/Show サイクルをやめることで前回フレームが一瞬残る問題を回避する。
        _points.Clear();
        _commandText = "";
        if (Visible)
            Invalidate();
    }

    private Point ToClient(int screenX, int screenY)
        => new(screenX - Bounds.Left, screenY - Bounds.Top);

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_mode == DrawMode.Command)
        {
            DrawCommand(e.Graphics);
            return;
        }

        if (_points.Count >= 2)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(_strokeColor, _strokeWidth)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            };
            e.Graphics.DrawLines(pen, _points.ToArray());
        }
    }

    private void DrawCommand(Graphics g)
    {
        if (string.IsNullOrEmpty(_commandText))
            return;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using var font = new Font(Font.FontFamily, 28, FontStyle.Bold);
        var size = g.MeasureString(_commandText, font);
        float x = (Width - size.Width) / 2f;
        float y = Height * 0.12f;
        var box = new RectangleF(x - 18, y - 10, size.Width + 36, size.Height + 20);
        // TransparencyKey は Magenta。背景は不透明の濃色ボックスで視認性を確保する。
        using var bg = new SolidBrush(Color.FromArgb(32, 32, 40));
        g.FillRectangle(bg, box);
        using var border = new Pen(_strokeColor, 2);
        g.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
        using var text = new SolidBrush(Color.White);
        g.DrawString(_commandText, font, text, x, y);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            string s = hex.TrimStart('#');
            if (s.Length == 6)
                return Color.FromArgb(
                    Convert.ToInt32(s[..2], 16),
                    Convert.ToInt32(s[2..4], 16),
                    Convert.ToInt32(s[4..6], 16));
        }
        catch (FormatException) { }
        return Color.FromArgb(0x80, 0xFF, 0x00);
    }
}
