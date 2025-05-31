using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class PartySizeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (int)value > 0 ? $"{parameter}{value}" : parameter;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}