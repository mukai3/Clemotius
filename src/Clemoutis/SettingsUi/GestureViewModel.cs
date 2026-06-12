using System.Collections.ObjectModel;
using Clemoutis.Core.Actions;
using Clemoutis.Core.Config;
using Clemoutis.Core.Gestures;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clemoutis.SettingsUi;

/// <summary>プロファイル1件のコンボ表示用ラッパー（リネームを表示へ反映するための INPC）。</summary>
internal sealed partial class ProfileItemViewModel : ObservableObject
{
    public MutableProfile Model { get; }

    [ObservableProperty] private string _displayName;

    public bool IsGlobal => Model.IsGlobal;

    public ProfileItemViewModel(MutableProfile model)
    {
        Model = model;
        _displayName = DisplayOf(model);
    }

    public void RefreshDisplay() => DisplayName = DisplayOf(Model);

    private static string DisplayOf(MutableProfile p)
        => p.IsGlobal
            ? $"{p.Name} (すべてのアプリ)"
            : $"{p.Name} ({(string.IsNullOrWhiteSpace(p.ProcessPattern) ? "未割当" : p.ProcessPattern)})";
}

/// <summary>ジェスチャー一覧の1行（矢印表記＋アクション説明）。</summary>
internal sealed record GestureRowViewModel(GestureBinding Binding)
{
    public string Arrows => new(Binding.Strokes
        .Select(c => c switch { 'U' => '↑', 'D' => '↓', 'L' => '←', 'R' => '→', _ => c })
        .ToArray());

    public string Description => ActionDisplay.Describe(Binding.Action);
}

/// <summary>
/// ジェスチャーページの編集状態。プロファイル群と選択中プロファイルの
/// ジェスチャー一覧・右ボタン+ホイール割当を扱う。
/// 変更はすべて <c>changed</c>（ルートの NotifyChanged）へ通知する。
/// </summary>
internal sealed partial class GestureViewModel : ObservableObject
{
    private readonly Action _changed;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = new();
    public ObservableCollection<GestureRowViewModel> Gestures { get; } = new();

    [ObservableProperty] private ProfileItemViewModel? _selectedProfile;
    [ObservableProperty] private string _wheelUpText = "(なし)";
    [ObservableProperty] private string _wheelDownText = "(なし)";
    [ObservableProperty] private bool _selectedIsRemovable;

    public GestureViewModel(IEnumerable<GestureProfile> profiles, Action changed)
    {
        _changed = changed;

        var list = profiles.Select(MutableProfile.From).ToList();
        NormalizeGlobalProfile(list);
        foreach (var p in list)
            Profiles.Add(new ProfileItemViewModel(p));
        SelectedProfile = Profiles[0];
    }

    /// <summary>グローバル("*")プロファイルを1つだけ先頭に固定する（旧画面と同じ規則）。</summary>
    private static void NormalizeGlobalProfile(List<MutableProfile> list)
    {
        var global = list.FirstOrDefault(p => p.IsGlobal)
            ?? new MutableProfile { Name = MutableProfile.GlobalName, ProcessPattern = "*" };
        global.Name = MutableProfile.GlobalName;
        list.RemoveAll(p => p.IsGlobal);
        list.Insert(0, global);
    }

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value) => RefreshFromSelected();

    private void RefreshFromSelected()
    {
        Gestures.Clear();
        if (SelectedProfile is not { } item)
        {
            SelectedIsRemovable = false;
            WheelUpText = WheelDownText = "(なし)";
            return;
        }
        foreach (var g in item.Model.Gestures)
            Gestures.Add(new GestureRowViewModel(g));
        SelectedIsRemovable = !item.IsGlobal;
        RefreshWheelTexts();
    }

    private void RefreshWheelTexts()
    {
        var p = SelectedProfile?.Model;
        WheelUpText = p?.WheelUp is { } u ? ActionDisplay.Describe(u) : "(なし)";
        WheelDownText = p?.WheelDown is { } d ? ActionDisplay.Describe(d) : "(なし)";
    }

    // ── プロファイル操作 ──

    public void AddProfile()
    {
        var item = new ProfileItemViewModel(
            new MutableProfile { Name = $"Profile{Profiles.Count}", ProcessPattern = "" });
        Profiles.Add(item);
        SelectedProfile = item;
        _changed();
    }

    public void RemoveSelectedProfile()
    {
        if (SelectedProfile is not { IsGlobal: false } item)
            return;
        Profiles.Remove(item);
        SelectedProfile = Profiles[0];
        _changed();
    }

    /// <summary>フライアウトの入力値を検証する。問題なければ null、あればエラーメッセージ。</summary>
    public static string? ValidateProfileEdit(string name, string processPattern, bool isGlobal)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "プロファイル名を入力してください。";
        if (!isGlobal && processPattern.Trim() == "*")
            return "「*」(すべてのアプリ) はグローバルプロファイル専用です。";
        return null;
    }

    /// <summary>フライアウトの「保存」。検証済みの値を選択中プロファイルへ反映する。</summary>
    public void ApplyProfileEdit(string name, string processPattern, bool gesturesEnabled)
    {
        if (SelectedProfile is not { } item)
            return;
        if (!item.IsGlobal)
        {
            item.Model.Name = name.Trim();
            item.Model.ProcessPattern = processPattern.Trim();
        }
        item.Model.GesturesEnabled = gesturesEnabled;
        item.RefreshDisplay();
        _changed();
    }

    // ── ジェスチャー操作 ──

    public void AddGesture(GestureBinding binding)
    {
        if (SelectedProfile is not { } item)
            return;
        item.Model.Gestures.Add(binding);
        Gestures.Add(new GestureRowViewModel(binding));
        _changed();
    }

    public void UpdateGesture(int index, GestureBinding binding)
    {
        if (SelectedProfile is not { } item || index < 0 || index >= item.Model.Gestures.Count)
            return;
        item.Model.Gestures[index] = binding;
        Gestures[index] = new GestureRowViewModel(binding);
        _changed();
    }

    public void RemoveGestureAt(int index)
    {
        if (SelectedProfile is not { } item || index < 0 || index >= item.Model.Gestures.Count)
            return;
        item.Model.Gestures.RemoveAt(index);
        Gestures.RemoveAt(index);
        _changed();
    }

    // ── 右ボタン + ホイール ──

    public GestureAction? WheelActionOf(bool up)
        => up ? SelectedProfile?.Model.WheelUp : SelectedProfile?.Model.WheelDown;

    public void SetWheelAction(bool up, GestureAction? action)
    {
        if (SelectedProfile is not { } item)
            return;
        if (up)
            item.Model.WheelUp = action;
        else
            item.Model.WheelDown = action;
        RefreshWheelTexts();
        _changed();
    }

    /// <summary>Build() 用: 現在の編集状態からプロファイル配列を構築する。</summary>
    public GestureProfile[] BuildProfiles() => Profiles.Select(p => p.Model.ToProfile()).ToArray();
}
