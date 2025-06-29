using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class TypeToVisibilityConverter : IValueConverter
{
    private readonly TypeToBooleanConverter _typeBooleanConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return _typeBooleanConverter.Convert(value, targetType, parameter, culture) is true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}