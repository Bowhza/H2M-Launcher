using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Models;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ServerViewModel : ObservableObject
    {
        public required IW4MServer Server { get; init; }

        [ObservableProperty]
        private double _id;

        [ObservableProperty]
        private string _version = "";

        [ObservableProperty]
        private string _game = "";

        [ObservableProperty]
        private string _hostName = "N/A";

        [ObservableProperty]
        private string _ip = "";

        [ObservableProperty]
        private int _port = 0;

        [ObservableProperty]
        private bool _isFavorite = false;

        [ObservableProperty]
        private string _gameType = "";

        [ObservableProperty]
        private string _gameTypeDisplayName = "";

        [ObservableProperty]
        private string _map = "";

        [ObservableProperty]
        private string _mapDisplayName = "";

        [ObservableProperty]
        private bool _hasMap = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Occupation))]
        private int _clientNum;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Occupation))]
        private int _maxClientNum;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Occupation))]
        private int _botsNum;

        [ObservableProperty]
        private long _ping = 999;

        [ObservableProperty]
        private bool _isPrivate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LastPlayed))]
        private DateTime? _joined;
        public string? SortPath => Joined?.ToString("s");

        public string LastPlayed
        {
            get
            {
                if (!Joined.HasValue)
                {
                    return "Never";
                }

                string ago = " ago";
                DateTime now = DateTime.Now;
                TimeSpan timespan = now - Joined.Value;
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

        public string Occupation => $"{ClientNum}/{MaxClientNum:D2} {"[" + BotsNum + "]", 4}";
    }
}
