using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Clemotius.SettingsUi;

/// <summary>件数(int)が 0 のとき Visible、それ以外は Collapsed を返す(一覧の空状態表示用)。</summary>
internal sealed class EmptyCountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
