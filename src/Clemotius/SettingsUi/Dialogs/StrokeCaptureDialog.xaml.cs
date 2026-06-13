using System.Windows;
using System.Windows.Input;
using Clemotius.Core.Gestures;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>
/// マウスをドラッグした軌跡からストローク列（U/D/L/R）を生成する入力ダイアログ（WPF版）。
/// 実行時のジェスチャー認識と同じ <see cref="StrokeEncoder"/> を使うため、
/// 入力結果が実際の判定と一致する。
/// </summary>
public partial class StrokeCaptureDialog
{
    private readonly StrokeEncoder _encoder = new(range: 16);
    private bool _capturing;

    public string? Result { get; private set; }

    public StrokeCaptureDialog(string? initial)
    {
        InitializeComponent();
        Preview.Text = string.IsNullOrEmpty(initial) ? "ここをドラッグしてストロークを描く" : ArrowsOf(initial);
        Result = string.IsNullOrEmpty(initial) ? null : initial;
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Trail.Points.Clear();
        _encoder.Reset();
        Preview.Text = "ここをドラッグしてストロークを描く";
        Result = null;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
            return;
        CanvasBorder.CaptureMouse(); // 右ボタンでも移動/解放を確実に受け取る
        _capturing = true;
        Trail.Points.Clear();
        var pt = e.GetPosition(TrailCanvas);
        Trail.Points.Add(pt);
        _encoder.Begin((int)pt.X, (int)pt.Y);
        Preview.Text = "";
        e.Handled = true; // 右クリックのコンテキストメニュー等を抑制
    }

    private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_capturing)
            return;
        var pt = e.GetPosition(TrailCanvas);
        Trail.Points.Add(pt);
        if (_encoder.Add((int)pt.X, (int)pt.Y))
            Preview.Text = ArrowsOf(_encoder.ToStrokeString());
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_capturing)
            return;
        CanvasBorder.ReleaseMouseCapture();
        _capturing = false;
        string strokes = _encoder.ToStrokeString();
        if (strokes.Length > 0)
        {
            Result = strokes;
            Preview.Text = ArrowsOf(strokes);
        }
        else
        {
            Preview.Text = "(ストロークなし)";
        }
        e.Handled = true;
    }

    /// <summary>U/D/L/R を矢印に変換してプレビュー表示する。</summary>
    private static string ArrowsOf(string strokes) => new(strokes
        .Select(c => c switch { 'U' => '↑', 'D' => '↓', 'L' => '←', 'R' => '→', _ => c })
        .ToArray());
}
