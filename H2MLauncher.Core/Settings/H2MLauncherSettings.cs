using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Settings
{
    public class H2MLauncherSettings
    {
        public string MWRLocation { get; set; } = string.Empty;

        public List<UserFavourite> FavouriteServers { get; set; } = [];
    }
}
