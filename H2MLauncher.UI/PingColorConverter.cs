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
            Brush pingColor;

            if (ping < 50)
                pingColor = Brushes.LawnGreen;
            else if (ping < 80)
                pingColor = Brushes.Orange;
            else
                pingColor = Brushes.Red;

            return pingColor;
        }
        
        throw new ArgumentNullException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}