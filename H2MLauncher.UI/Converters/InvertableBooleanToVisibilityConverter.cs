using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InvertableBooleanToVisibilityConverter : IValueConverter
    {
        enum Parameters
        {
            Normal, Inverted
        }

        public object? Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value is not bool boolValue)
            {
                return null;
            }

            Parameters direction = parameter is not string parameterString ? Parameters.Normal : Enum.Parse<Parameters>(parameterString);

            if (direction == Parameters.Inverted)
                return !boolValue ? Visibility.Visible : Visibility.Collapsed;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object? ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
