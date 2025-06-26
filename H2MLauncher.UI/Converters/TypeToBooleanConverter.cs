using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class TypeToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        Type dataType = value.GetType();
        Type? expectedType = parameter as Type;

        return expectedType is not null && expectedType.IsAssignableFrom(dataType);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
