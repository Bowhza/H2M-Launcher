using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    // RecentServerViewModel will have every thing the default ServerViewModel will have + when the user joined a server
    public partial class RecentServerViewModel : ServerViewModel, IServerViewModel
    {
        [ObservableProperty]
        private DateTime _joined = DateTime.Now;

        public string LastPlayed 
        { 
            get
            {
                string ago = " ago";
                DateTime now = DateTime.Now;
                TimeSpan timespan = now - Joined;
                if (timespan.TotalSeconds < 60)
                    return $"{(int)timespan.TotalSeconds}s{ago}";
                if (timespan.TotalMinutes < 60)
                    return $"{(int)timespan.TotalMinutes}m{ago}";
                if (timespan.TotalHours < 24)
                    return $"{(int)timespan.TotalHours}h{ago}";
                if (timespan.TotalDays < 7)
                    return $"{(int)timespan.TotalDays}d{ago}";
                int weeks = (int)(timespan.TotalDays / 7);
                return $"{weeks}w{ago}";
            }
        }
    }
}
