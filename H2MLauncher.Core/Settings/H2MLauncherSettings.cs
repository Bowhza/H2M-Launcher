using H2MLauncher.Core.Models;
using H2MLauncher.Core.Social.Player;

namespace H2MLauncher.Core.Settings
{
    public record H2MLauncherSettings
    {
        public string MWRLocation { get; init; } = string.Empty;

        public string IW4MMasterServerUrl { get; init; } = string.Empty;

        public string HMWMasterServerUrl { get; init; } = string.Empty;

        public List<SimpleServerInfo> FavouriteServers { get; init; } = [];

        public List<RecentServerInfo> RecentServers { get; init; } = [];

        public List<RecentPlayerInfo> RecentPlayers { get; init; } = [];

        public ServerFilterSettings ServerFilter { get; init; } = new();

        public bool AutomaticGameDetection { get; init; } = true;

        public bool GameMemoryCommunication { get; init; } = false;

        public bool WatchGameDirectory { get; init; } = true;

        public bool ServerQueueing { get; init; } = false;

        public bool PublicPlayerName { get; init; } = true;

        public Dictionary<string, string> KeyBindings { get; init; } = [];
        public LauncherCustomizationSettings? Customization { get; init; } = null;
    }

    public record LauncherCustomizationSettings
    {
        public bool HotReloadThemes { get; init; } = true;
        public string? BackgroundImagePath { get; init; } = null;
        public double? BackgroundBlur { get; init; } = null;
        public List<string>? Themes { get; init; } = null;
    }
}
