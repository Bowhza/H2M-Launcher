using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class StringUriConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string uriString && 
            Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return uri;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Uri uri)
        {
            return uri.ToString();
        }

        return null;
    }
}