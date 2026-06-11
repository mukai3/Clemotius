using Clemoutis.Core.Gestures;

namespace Clemoutis.Settings;

/// <summary>
/// マウスをドラッグした軌跡からストローク列（U/D/L/R）を生成する入力ダイアログ。
/// 実行時のジェスチャー認識と同じ <see cref="StrokeEncoder"/> を使うため、
/// 入力結果が実際の判定と一致する。
/// </summary>
internal sealed class StrokeCaptureDialog : Form
{
    private readonly StrokeEncoder _encoder = new(range: 16);
    private readonly Panel _canvas = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly Label _preview = new() { Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
    private readonly List<Point> _trail = new();
    private bool _capturing;

    public string? Result { get; private set; }

    public StrokeCaptureDialog(string? initial)
    {
        Text = "ストロークの入力";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(360, 320);

        _preview.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
        _preview.Text = string.IsNullOrEmpty(initial) ? "ここを左ドラッグでストロークを描く" : initial;

        var hint = new Label
        {
            Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleCenter,
            Text = "左ボタンでドラッグ → 離すと確定",
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Width = 80 };
        var clear = new Button { Text = "クリア", Width = 80 };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6),
        };
        clear.Click += (_, _) => ResetTrail();
        buttons.Controls.AddRange(new Control[] { cancel, ok, clear });

        _canvas.MouseDown += OnCanvasMouseDown;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseUp += OnCanvasMouseUp;
        _canvas.Paint += OnCanvasPaint;

        Controls.Add(_canvas);
        Controls.Add(hint);
        Controls.Add(buttons);
        Controls.Add(_preview);
        AcceptButton = ok;
        CancelButton = cancel;

        Result = string.IsNullOrEmpty(initial) ? null : initial;
    }

    private void ResetTrail()
    {
        _trail.Clear();
        _encoder.Reset();
        _preview.Text = "ここを左ドラッグでストロークを描く";
        Result = null;
        _canvas.Invalidate();
    }

    private void OnCanvasMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _capturing = true;
        _trail.Clear();
        _trail.Add(e.Location);
        _encoder.Begin(e.X, e.Y);
        _preview.Text = "";
        _canvas.Invalidate();
    }

    private void OnCanvasMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_capturing) return;
        _trail.Add(e.Location);
        if (_encoder.Add(e.X, e.Y))
            _preview.Text = ArrowsOf(_encoder.ToStrokeString());
        _canvas.Invalidate();
    }

    private void OnCanvasMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_capturing) return;
        _capturing = false;
        string strokes = _encoder.ToStrokeString();
        if (strokes.Length > 0)
        {
            Result = strokes;
            _preview.Text = ArrowsOf(strokes);
        }
        else
        {
            _preview.Text = "（ストロークなし）";
        }
    }

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        if (_trail.Count < 2) return;
        using var pen = new Pen(Color.RoyalBlue, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, _trail.ToArray());
    }

    /// <summary>U/D/L/R を矢印に変換してプレビュー表示する。</summary>
    private static string ArrowsOf(string strokes)
    {
        var chars = strokes.Select(c => c switch
        {
            'U' => '↑', 'D' => '↓', 'L' => '←', 'R' => '→', _ => c,
        });
        return new string(chars.ToArray());
    }
}
