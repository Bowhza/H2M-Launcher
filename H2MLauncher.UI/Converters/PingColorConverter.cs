using System.Globalization;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace H2MLauncher.UI;

public class PingColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long ping)
        {
            return ping switch
            {
                < 50 => Brushes.LawnGreen,
                < 80 => Brushes.Orange,
                _ => Brushes.Red,
            };
        }

        throw new ArgumentNullException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DialogViewModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long ping)
        {
            return ping switch
            {
                < 50 => Brushes.LawnGreen,
                < 80 => Brushes.Orange,
                _ => Brushes.Red,
            };
        }

        throw new ArgumentNullException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}