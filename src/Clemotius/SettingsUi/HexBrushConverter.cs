using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Clemotius.SettingsUi;

/// <summary>"#RRGGBB" 文字列を SolidColorBrush に変換する（色見本表示用）。</summary>
internal sealed class HexBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex
                && System.Windows.Media.ColorConverter.ConvertFromString(hex)
                    is System.Windows.Media.Color color)
            {
                return new SolidColorBrush(color);
            }
        }
        catch (FormatException) { }
        return System.Windows.Media.Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
