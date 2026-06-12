using CommunityToolkit.Mvvm.ComponentModel;

namespace Clemoutis.SettingsUi;

/// <summary>
/// 「ラベル＋選択肢コンボ」1行分の編集スロット。値が変わると <c>changed</c>
/// （ルートの <see cref="SettingsViewModel.NotifyChanged"/>）を呼ぶ。
/// 拡張スクロール/ホイール/タイトルバーのコンボ行を共通化する。
/// </summary>
internal sealed class ChoiceSlotViewModel : ObservableObject
{
    private readonly Action _changed;
    private string _value;

    /// <summary>Build() で設定欄と対応付けるためのキー（例 "Shift", "RightClick"）。</summary>
    public string Key { get; }

    public string Label { get; }

    public IReadOnlyList<ScrollBehaviorChoice.Choice> Choices { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
                _changed();
        }
    }

    public ChoiceSlotViewModel(
        string key, string label, IReadOnlyList<ScrollBehaviorChoice.Choice> choices,
        string initial, Action changed)
    {
        Key = key;
        Label = label;
        Choices = choices;
        _changed = changed;
        // 未知値は先頭（なし）にフォールバック（旧画面と同じ規則）
        _value = choices.Any(c => c.Value == initial) ? initial : choices[0].Value;
    }
}
