using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI;

public class EmptyItemConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value ?? parameter;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value.Equals(parameter) ? null : value;
    }
}
