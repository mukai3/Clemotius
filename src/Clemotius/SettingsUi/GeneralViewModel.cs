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

    [ObservableProperty] private int _themeIndex; // 0=システム連動, 1=ライト, 2=ダーク
    [ObservableProperty] private int _trayDoubleClickAction; // 0=設定を開く, 1=一時停止
    [ObservableProperty] private int _range;

    /// <summary>テーマの設定値("system"/"light"/"dark")。ThemeIndex から導出する。</summary>
    public string Theme => ThemeIndex switch { 1 => "light", 2 => "dark", _ => "system" };
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
        _themeIndex = config.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        _trayDoubleClickAction = config.Tray.DoubleClickAction == 1 ? 1 : 0;
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
