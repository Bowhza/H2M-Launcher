using System.Globalization;
using System.Windows.Media;

namespace H2MLauncher.UI.Converters;

public class UriImageSourceConverter : UrlCombinerConverter
{
    private static readonly ImageSourceConverter ImageSourceConverter = new();

    public override object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0) return null;
        if (values[^1] is ImageSource imageSource)
        {
            return imageSource;
        }

        object? imageUri = base.Convert(values, targetType, parameter, culture);
        if (imageUri is null)
        {
            return null;
        }

        return ImageSourceConverter.ConvertFrom(imageUri);
    }
}