using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ServerViewModel : ObservableObject, IServerViewModel
    {
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

        public string Occupation => $"{ClientNum}/{MaxClientNum:D2} {"[" + BotsNum + "]", 4}";
    }

    public interface IServerViewModel
    {
        public double Id { get; set; }
        public string Version { get; set; }
        public string Game { get; set; }
        public string HostName { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string GameType { get; set; }
        public string GameTypeDisplayName { get; set; }
        public string Map { get; set; }
        public string MapDisplayName { get; set; }
        public int ClientNum { get; set; }
        public int MaxClientNum { get; set; }
        public int BotsNum { get; set; }
        public long Ping { get; set; }
        public bool IsPrivate { get; set; }
        public string Occupation { get; }
        public bool IsFavorite { get; set; }
    }
}
