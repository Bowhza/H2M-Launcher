using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ServerViewModel : ObservableObject
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
        private string _map = "";

        [ObservableProperty]
        private bool _isFavorite = false;

        [ObservableProperty]
        private string _gameType = "";

        [ObservableProperty]
        private string _mapDisplayName = "";

        [ObservableProperty]
        private string _gameTypeDisplayName = "";

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

        public override string ToString()
        {
            return $"{HostName} ({Ip}:{Port})";
        }
    }
}
