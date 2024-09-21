using System.Windows.Data;

namespace H2MLauncher.UI.Converters
{
    public class ProtocolToIconConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int protocol)
            {
                return protocol switch
                {
                    3 => "Assets/hmw.png",
                    2 => "Assets/h2m.png",
                    _ => null
                };
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
