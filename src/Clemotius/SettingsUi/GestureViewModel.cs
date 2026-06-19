using System.Collections.ObjectModel;
using Clemotius.Core.Actions;
using Clemotius.Core.Config;
using Clemotius.Core.Gestures;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clemotius.SettingsUi;

/// <summary>プロファイル1件のコンボ表示用ラッパー（リネームを表示へ反映するための INPC）。</summary>
internal sealed partial class ProfileItemViewModel : ObservableObject
{
    public MutableProfile Model { get; }

    [ObservableProperty] private string _displayName;

    public ProfileItemViewModel(MutableProfile model)
    {
        Model = model;
        _displayName = DisplayOf(model);
    }

    public void RefreshDisplay() => DisplayName = DisplayOf(Model);

    private static string DisplayOf(MutableProfile p)
        => $"{p.Name} ({(string.IsNullOrWhiteSpace(p.ProcessPattern) ? "未割当" : p.ProcessPattern)})";
}

/// <summary>ジェスチャー一覧の1行（トリガ表記＝矢印 or ホイール＋アクション説明）。</summary>
internal sealed record GestureRowViewModel(GestureBinding Binding)
{
    public string Arrows => Binding.Strokes switch
    {
        WheelStrokes.Up => "ホイール↑",
        WheelStrokes.Down => "ホイール↓",
        _ => new(Binding.Strokes
            .Select(c => c switch { 'U' => '↑', 'D' => '↓', 'L' => '←', 'R' => '→', _ => c })
            .ToArray()),
    };

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
    [ObservableProperty] private bool _selectedIsRemovable;

    public GestureViewModel(IEnumerable<GestureProfile> profiles, Action changed)
    {
        _changed = changed;

        foreach (var p in profiles.Select(MutableProfile.From))
            Profiles.Add(new ProfileItemViewModel(p));
        SelectedProfile = Profiles.FirstOrDefault();
    }

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value) => RefreshFromSelected();

    private void RefreshFromSelected()
    {
        Gestures.Clear();
        if (SelectedProfile is not { } item)
        {
            SelectedIsRemovable = false;
            return;
        }
        foreach (var g in item.Model.Gestures)
            Gestures.Add(new GestureRowViewModel(g));
        SelectedIsRemovable = true;
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
        if (SelectedProfile is not { } item)
            return;
        Profiles.Remove(item);
        SelectedProfile = Profiles.FirstOrDefault();
        _changed();
    }

    /// <summary>フライアウトの入力値を検証する。問題なければ null、あればエラーメッセージ。</summary>
    public static string? ValidateProfileEdit(string name, string processPattern)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "プロファイル名を入力してください。";
        if (string.IsNullOrWhiteSpace(processPattern))
            return "対象プロセスを入力してください。";
        return null;
    }

    /// <summary>フライアウトの「保存」。検証済みの値を選択中プロファイルへ反映する。</summary>
    public void ApplyProfileEdit(string name, string processPattern, bool gesturesEnabled)
    {
        if (SelectedProfile is not { } item)
            return;
        item.Model.Name = name.Trim();
        item.Model.ProcessPattern = processPattern.Trim();
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

    /// <summary>
    /// 選択中プロファイルで既に使われているストローク（WU/WD 含む）。追加/編集ダイアログで
    /// 重複登録を防ぐために渡す。<paramref name="exceptIndex"/> は編集対象の行を除外する
    /// （自分自身のストロークは保ってよい）ため。
    /// </summary>
    public IReadOnlyCollection<string> StrokesInUse(int exceptIndex = -1)
    {
        if (SelectedProfile is not { } item)
            return Array.Empty<string>();
        return item.Model.Gestures
            .Where((b, i) => i != exceptIndex)
            .Select(b => b.Strokes)
            .ToArray();
    }

    /// <summary>Build() 用: 現在の編集状態からプロファイル配列を構築する。</summary>
    public GestureProfile[] BuildProfiles() => Profiles.Select(p => p.Model.ToProfile()).ToArray();
}
