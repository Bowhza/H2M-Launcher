using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace H2MLauncher.UI
{
    public class HostNameColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string hostname = (string)value;
            List<Run> runs = [];
            MatchCollection matches = Regex.Matches(hostname, @"(\^\d|\^\:)([^\^]*?)(?=\^\d|\^:|$)");
            if (matches.Any())
            {
                if (matches[0].Index != 0)
                {
                    runs.Add(new Run() { Text = hostname[..matches[0].Index], Foreground = Brushes.White, FlowDirection = System.Windows.FlowDirection.LeftToRight });
                }
                foreach (Match match in matches)
                {
                    string text = match.Groups[1].Value;
                    Brush brush = text switch
                    {
                        "^0" => Brushes.Black,
                        "^1" => Brushes.Red,
                        "^2" => Brushes.Green,
                        "^3" => Brushes.Yellow,
                        "^4" => Brushes.Blue,
                        "^5" => Brushes.Cyan,
                        "^6" => Brushes.Magenta,
                        "^7" => Brushes.White,
                        "^8" => Brushes.Black,
                        _ => Brushes.White, // ^: rainbow
                    };
                    runs.Add(new Run() { Text = match.Groups[2].Value, Foreground = brush, FlowDirection = System.Windows.FlowDirection.LeftToRight });
                }
            }
            else
            {
                runs.Add(new Run() { Text = hostname, Foreground = Brushes.White, FlowDirection = System.Windows.FlowDirection.LeftToRight });
            }
            return runs.ToArray();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
