using System.IO;

namespace H2MLauncher.UI
{
    public static class Constants
    {
        public static readonly string LocalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterH2MLauncher");

        public static readonly string LogFilePath = Path.Combine(LocalDir, "log.txt");

        public static readonly string LauncherSettingsFile = Path.Combine(LocalDir, "launchersettings.json");

        public const string LauncherSettingsSection = "H2MLauncher";

        public const string ResourceSection = "Resource";
    }
}
