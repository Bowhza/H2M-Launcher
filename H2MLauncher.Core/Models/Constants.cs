using System.IO;

namespace H2MLauncher.UI
{
    public static class Constants
    {
        public static readonly string LocalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterH2MLauncher");

        public static readonly string LogFilePath = Path.Combine(LocalDir, "log.txt");
    }
}
