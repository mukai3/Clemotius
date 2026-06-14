using System.Collections.ObjectModel;
using System.ComponentModel;
using Clemotius.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    [ObservableProperty] private bool _showBalloonTip;
    [ObservableProperty] private int _range;
    [ObservableProperty] private int _timeoutMs;
    [ObservableProperty] private int _pushHoldTimeMs;
    [ObservableProperty] private bool _drawStroke;
    [ObservableProperty] private int _drawingType;
    [ObservableProperty] private int _strokeWidth;
    [ObservableProperty] private string _validStrokeColor = "#000000";
    [ObservableProperty] private string _invalidStrokeColor = "#000000";

    /// <summary>ジェスチャーを無効にする（アプリ側へ右ボタンを透過する）プロセス名。</summary>
    public ObservableCollection<string> ExcludedProcesses { get; } = new();

    /// <summary>追加用テキストボックスの入力値。</summary>
    [ObservableProperty] private string _newProcessName = "";

    public GeneralViewModel(ClemotiusConfig config, Action changed)
    {
        _changed = changed;
        foreach (var name in config.Gesture.ExcludedProcesses)
            ExcludedProcesses.Add(name);
        _showTrayIcon = config.Tray.ShowTrayIcon;
        _showBalloonTip = config.Tray.ShowBalloonTip;
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

    [RelayCommand]
    private void AddExcludedProcess()
    {
        string name = ProcessName.Normalize(NewProcessName);
        if (name.Length == 0)
            return;
        // 大文字小文字を無視して重複登録を防ぐ
        if (ExcludedProcesses.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewProcessName = "";
            return;
        }
        ExcludedProcesses.Add(name);
        NewProcessName = "";
        _changed();
    }

    [RelayCommand]
    private void RemoveExcludedProcess(string? name)
    {
        if (name is null)
            return;
        if (ExcludedProcesses.Remove(name))
            _changed();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // NewProcessName は追加用の一時入力なので保存トリガーにしない
        if (_initialized && e.PropertyName != nameof(NewProcessName))
            _changed();
    }
}
