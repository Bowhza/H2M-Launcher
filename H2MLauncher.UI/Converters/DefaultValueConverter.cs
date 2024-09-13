using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace H2MLauncher.UI.Converters;

public class DefaultValueConverter : MarkupExtension, IValueConverter
{
    public object? DefaultValue { get; set; }


    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Equals(value, DefaultValue))
        {
            return parameter;
        }

        return value;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Equals(value, parameter))
        {
            return DefaultValue;
        }

        return value;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}