using System.IO;

namespace H2MLauncher.UI
{
    public static class Constants
    {
        public static readonly string LocalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterH2MLauncher");

        public static readonly string LogFilePath = Path.Combine(LocalDir, "log.txt");

        public static readonly string LauncherSettingsFileName = "launchersettings.json";

        public static readonly string LauncherSettingsFilePath = Path.Combine(LocalDir, LauncherSettingsFileName);

        public static readonly string KeyFilePath = Path.Combine(LocalDir, "userkey");

        /// <summary>
        /// The key of the <see cref="Core.Settings.H2MLauncherSettings"/> section in the configuration.
        /// </summary>
        public const string LauncherSettingsSection = "H2MLauncher";

        /// <summary>
        /// The key of the <see cref="Core.Settings.ResourceSettings"/> section in the configuration.
        /// </summary>
        public const string ResourceSection = "Resource";

        /// <summary>
        /// The key of the <see cref="Core.Settings.MatchmakingSettings"/> section in the configuration.
        /// </summary>
        public const string MatchmakingSection = "Matchmaking";

        /// <summary>
        /// The injection key for the default <see cref="Core.Settings.H2MLauncherSettings"/>.
        /// </summary>
        public const string DefaultSettingsKey = "DefaultSettings";


        public const string DISCORD_INVITE_LINK = "https://discord.gg/J6cxWGvy4C";

        public const string GITHUB_REPO = "https://github.com/Bowhza/H2M-Launcher";

        // Resources
        public const string BackgroundImageSourceKey = "BackgroundImageSource";
        public const string BackgroundImageBlurRadiusKey = "BackgroundImageBlurRadius";
        public const string BackgroundVideoSourceKey = "BackgroundVideoSource";
        public const string CurrentThemeDirectoryKey = "CurrentThemeDirectory";
    }
}
