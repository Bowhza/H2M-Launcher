using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;

namespace H2MLauncher.UI
{
    public class MapColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string folderName)
            {
                // Define the directory where map folders are located
                string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string mapsDirectory = Path.Combine(executableDirectory, "h2m-usermaps");

                // Construct the full path to the folder
                string folderPath = Path.Combine(mapsDirectory, folderName);

                System.Diagnostics.Debug.WriteLine($"Checking folder: {folderPath}");

                // Check if the folder exists
                bool folderExists = Directory.Exists(folderPath);

                System.Diagnostics.Debug.WriteLine($"Folder exists: {folderExists}");

                // Return Green if the folder exists, Red if it doesn't
                return folderExists ? Brushes.Green : Brushes.Red;
            }

            // Return default color if the value is not a string
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // No need to implement ConvertBack for this use case
            throw new NotImplementedException();
        }
    }
}
