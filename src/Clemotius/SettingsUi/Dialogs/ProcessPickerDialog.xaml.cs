using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Clemotius.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>
/// 実行中プロセスの一覧から対象アプリを複数選択するダイアログ。
/// 既存テキストに含まれる名前は初期チェック済みとして表示し（起動していなくても候補に残す）、
/// OK で確定した全選択をカンマ区切りテキスト <see cref="Result"/> として返す。
/// </summary>
public partial class ProcessPickerDialog
{
    /// <summary>1件の選択候補。チェック状態は双方向バインドする。</summary>
    internal sealed partial class Item : ObservableObject
    {
        public string Name { get; }
        public string Title { get; }
        [ObservableProperty] private bool _isSelected;

        public Item(string name, string title, bool selected)
        {
            Name = name;
            Title = title;
            _isSelected = selected;
        }
    }

    private readonly List<Item> _items;
    private readonly ICollectionView _view;

    /// <summary>OK 確定時に選択された全プロセス名をカンマ区切りで整形したテキスト。</summary>
    public string Result { get; private set; } = "";

    public ProcessPickerDialog(string? currentText)
    {
        InitializeComponent();

        var current = ProcessNameList.Parse(currentText);
        var selected = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);

        var running = RunningProcesses.WithVisibleWindows();
        var runningNames = new HashSet<string>(running.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

        _items = new List<Item>();
        // 既に登録済みだが起動していない名前も候補へ（チェック維持のため）
        foreach (var name in current)
            if (!runningNames.Contains(name))
                _items.Add(new Item(name, "(起動していません)", selected: true));
        foreach (var r in running)
            _items.Add(new Item(r.Name, r.Title, selected: selected.Contains(r.Name)));

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterItem;
        List.ItemsSource = _view;
    }

    private bool FilterItem(object obj)
    {
        if (Filter.Text is not { Length: > 0 } q || obj is not Item item)
            return true;
        return item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Title.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => _view.Refresh();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = ProcessNameList.Format(_items.Where(i => i.IsSelected).Select(i => i.Name));
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
