using System.Globalization;
using System.Windows.Data;

using Humanizer;

namespace H2MLauncher.UI.Converters;

public class HumanizeDateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Humanize();
        }

        if (value is DateTime dateTime)
        {
            return dateTime.Humanize();
        }

        if (value is TimeSpan timeSpan)
        {
            return timeSpan.Humanize();
        }

        return value.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}