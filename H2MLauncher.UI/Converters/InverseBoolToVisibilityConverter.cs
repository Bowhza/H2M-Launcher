using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool boolValue)
            {
                return null;
            }

            return !boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
