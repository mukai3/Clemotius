using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clemotius.Core.Config;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Clemotius.Gestures;

/// <summary>
/// ジェスチャー軌跡を描画する WPF オーバーレイの管理クラス。
/// ジェスチャーごとに専用の透明ウィンドウを生成し、終了時に閉じる。
///
/// 全画面の透明レイヤードウィンドウを「出しっぱなし」にすると、モニタのスリープ復帰・
/// ロック解除・GPU ドライバリセット等で描画面が稀に壊れて灰色化し、閉じないため画面に
/// 残り続ける（復帰不能）。逆に Hide/Show で使い回すとレイヤードウィンドウが前回フレーム
/// （前回の軌跡）を一瞬残す。都度生成・都度破棄なら、毎回まっさらな描画面になり前フレーム
/// 残りが無く、待機中はウィンドウが存在しないため灰色化が残らない。
///
/// フックスレッドからは直接呼ばず、UI スレッドへマーシャルしてから呼ぶこと。
/// </summary>
internal sealed class GestureWpfOverlay
{
    private double _strokeWidth = 2;
    private WpfColor _strokeColor = WpfColor.FromRgb(0x80, 0xFF, 0x00);
    private TrailWindow? _window;

    public void ApplySettings(GestureSettings g)
    {
        _strokeWidth = Math.Max(1, g.StrokeWidth);
        _strokeColor = OverlayNative.ParseColor(g.ValidStrokeColor);
    }

    /// <summary>ジェスチャー開始。新しい全画面ウィンドウを生成して軌跡を描き始める（物理ピクセル座標）。</summary>
    public void Begin(int physX, int physY)
    {
        _window?.Close();
        _window = new TrailWindow(_strokeColor, _strokeWidth);
        _window.Begin(physX, physY);
        _window.Show();
    }

    public void AddPoint(int physX, int physY) => _window?.AddPoint(physX, physY);

    /// <summary>ジェスチャー終了。ウィンドウを閉じる（描画面ごと破棄）。</summary>
    public void End()
    {
        _window?.Close();
        _window = null;
    }

    /// <summary>アプリ終了時の後始末。</summary>
    public void Close()
    {
        _window?.Close();
        _window = null;
    }

    /// <summary>1 ジェスチャーぶんの軌跡を描く使い捨ての透明ウィンドウ。</summary>
    private sealed class TrailWindow : Window
    {
        private readonly Canvas _canvas = new();
        private readonly Polyline _line;
        private double _dpiScale = 1.0;

        public TrailWindow(WpfColor color, double width)
        {
            // アプリ全体の暗黙 Window スタイル（WPF-UI テーマ）を継承しない。継承すると
            // テーマ背景でウィンドウ全体が塗られ、透明オーバーレイが灰色全画面化してしまう。
            Style = new Style(typeof(Window));

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = WpfBrushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            Topmost = true;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Content = _canvas;

            _line = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = width,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            _canvas.Children.Add(_line);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            OverlayNative.MakeClickThrough(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }

        /// <summary>全画面に広げ、開始点を打つ（Show 前に呼ぶ）。</summary>
        public void Begin(int physX, int physY)
        {
            _dpiScale = OverlayNative.GetDpiScaleAtPoint(physX, physY);
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            _line.Points.Add(ToCanvas(physX, physY));
        }

        public void AddPoint(int physX, int physY) => _line.Points.Add(ToCanvas(physX, physY));

        private WpfPoint ToCanvas(int physX, int physY)
            => new(physX / _dpiScale - Left, physY / _dpiScale - Top);
    }
}
