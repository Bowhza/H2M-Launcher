using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class BooleanToStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isFavorite)
        {
            return isFavorite ? "★" : "☆";
        }
        return "☆";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
