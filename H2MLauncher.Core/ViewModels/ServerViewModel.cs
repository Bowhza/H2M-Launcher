using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.ViewModels
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
        private string _gameType = "";

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

        public string Occupation => $"{ClientNum}/{MaxClientNum:D2} {"[" + BotsNum + "]",4}";

        public IW4MServer Server { get; }

        public ServerViewModel(IW4MServer server)
        {
            Server = server;
        }

        public override string ToString()
        {
            return $"{HostName} ({Ip}:{Port})";
        }
    }
}
