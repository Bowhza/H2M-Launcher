using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

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
        [NotifyPropertyChangedFor(nameof(PingDisplay))]
        private long _ping = -1;

        [ObservableProperty]
        private bool _isPrivate;

        public string Occupation => $"{ClientNum:D2}/{MaxClientNum:D2}";
        public string PingDisplay => Ping == -1 ? "N/A" : $"{Ping:D3}";
    }
}
