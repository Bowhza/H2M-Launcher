using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class NegatingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double)
        {
            return -((double)value);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double)
        {
            return +(double)value;
        }
        return value;
    }
}