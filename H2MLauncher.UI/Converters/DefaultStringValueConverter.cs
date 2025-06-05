using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class DefaultStringValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string stringValue || string.IsNullOrEmpty(stringValue))
        {
            return parameter;
        }

        return value;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Equals(value, parameter))
        {
            return null;
        }

        return value;
    }
}