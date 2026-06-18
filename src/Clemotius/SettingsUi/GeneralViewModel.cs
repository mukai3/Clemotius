using System.ComponentModel;
using Clemotius.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clemotius.SettingsUi;

/// <summary>
/// 一般ページの編集状態（トレイ設定＋ジェスチャー詳細）。
/// どのプロパティが変わってもルートへ変更通知して即時適用に乗せる。
/// </summary>
internal sealed partial class GeneralViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _initialized;

    [ObservableProperty] private bool _showTrayIcon;
    [ObservableProperty] private int _range;
    [ObservableProperty] private int _timeoutMs;
    [ObservableProperty] private int _pushHoldTimeMs;
    [ObservableProperty] private bool _drawStroke;
    [ObservableProperty] private int _drawingType;
    [ObservableProperty] private int _strokeWidth;
    [ObservableProperty] private string _validStrokeColor = "#000000";
    [ObservableProperty] private string _invalidStrokeColor = "#000000";

    public GeneralViewModel(ClemotiusConfig config, Action changed)
    {
        _changed = changed;
        _showTrayIcon = config.Tray.ShowTrayIcon;
        _range = Math.Clamp(config.Gesture.Range, 1, 100);
        _timeoutMs = Math.Clamp(config.Gesture.TimeoutMs, 0, 10000);
        _pushHoldTimeMs = Math.Clamp(config.Gesture.PushHoldTimeMs, 0, 5000);
        _drawStroke = config.Gesture.DrawStroke;
        _drawingType = config.Gesture.DrawingType == 1 ? 1 : 0;
        _strokeWidth = Math.Clamp(config.Gesture.StrokeWidth, 1, 20);
        _validStrokeColor = config.Gesture.ValidStrokeColor;
        _invalidStrokeColor = config.Gesture.InvalidStrokeColor;
        _initialized = true;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_initialized)
            _changed();
    }
}
