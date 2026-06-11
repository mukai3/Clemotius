using Clemoutis.Core.Config;

namespace Clemoutis.Gestures;

/// <summary>
/// ジェスチャーの軌跡を画面全体に重ねて描画する、クリック透過のトップモスト オーバーレイ。
/// マウス入力を一切受けない（WS_EX_TRANSPARENT）。TransparencyKey で背景を透明化する。
/// </summary>
internal sealed class GestureTrailOverlay : Form
{
    private static readonly Color TransparentKey = Color.Magenta;

    private readonly List<Point> _points = new();
    private Color _strokeColor = Color.FromArgb(0x80, 0xFF, 0x00);
    private int _strokeWidth = 2;

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
    }

    /// <summary>ジェスチャー開始。全画面に広げて軌跡をリセットする。</summary>
    public void Begin(int screenX, int screenY)
    {
        Bounds = SystemInformation.VirtualScreen;
        _points.Clear();
        _points.Add(ToClient(screenX, screenY));
        if (!Visible)
        {
            // 表示する前に空状態を描画して、前回の軌跡が一瞬見えるのを防ぐ
            Invalidate();
            Update();
            Visible = true;
        }
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
        _points.Clear();
        if (Visible)
            Visible = false;
    }

    private Point ToClient(int screenX, int screenY)
        => new(screenX - Bounds.Left, screenY - Bounds.Top);

    protected override void OnPaint(PaintEventArgs e)
    {
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
