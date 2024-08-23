using System.Globalization;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace H2MLauncher.UI;

public class PingColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long ping = (long)value;
        Brush pingColor;

        if (ping < 50)
            pingColor = Brushes.LawnGreen;
        else if (ping < 80)
            pingColor = Brushes.Orange;
        else
            pingColor = Brushes.Red;

        return pingColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}