using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Settings
{
    public record H2MLauncherSettings
    {
        public string MWRLocation { get; init; } = string.Empty;

        public string IW4MMasterServerUrl { get; init; } = string.Empty;

        public List<UserFavourite> FavouriteServers { get; init; } = [];

        public ServerFilterSettings ServerFilter { get; init; } = new();
    }
}
