using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class UrlCombinerConverter : IMultiValueConverter
{
    public virtual object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null ||
            values.Length < 2 ||
            values[1] is not string path ||
            values[0] is not string basePath)
        {
            return null;
        }

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (!Uri.TryCreate(path.TrimStart('/', '\\'), UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return null;
        }

        if (uri.IsAbsoluteUri)
        {
            // its an absolute uri
            return uri;
        }

        var uriBuilder = new UriBuilder(basePath.TrimEnd('/', '\\'));

        uriBuilder.Path += "/";
        uriBuilder.Path += uri;

        return uriBuilder.Uri;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
