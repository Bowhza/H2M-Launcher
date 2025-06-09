using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class StringUriConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string uriString)
        {
            return null;
        }

        if (parameter is string baseUriString)
        {
            if (!Uri.TryCreate(baseUriString, UriKind.RelativeOrAbsolute, out Uri? baseUri))
            {
                return null;
            }

            if (Uri.TryCreate(baseUri, uriString, out Uri? combinedUri))
            {
                return combinedUri;
            }

            return null;
        }

        if (parameter is Uri baseUriParameter)
        {
            if (Uri.TryCreate(baseUriParameter, uriString, out Uri? combinedUri))
            {
                return combinedUri;
            }

            return null;
        }

        if (Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out Uri? uri))
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