using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Settings
{
    public class H2MLauncherSettings
    {
        public string MWRLocation { get; set; } = string.Empty;

        public string IW4MMasterServerUrl { get; set; } = string.Empty;

        public List<SimpleServerInfo> FavouriteServers { get; set; } = [];

        public List<RecentServerInfo> RecentServers { get; set; } = [];
    }
}
