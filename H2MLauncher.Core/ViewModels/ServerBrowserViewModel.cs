using System.Collections.ObjectModel;
using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;

namespace H2MLauncher.Core.ViewModels
{
    public class ServerBrowserViewModel : ObservableObject
    {
        private readonly RaidMaxService _raidMaxService;
        private readonly GameServerCommunicationService _serverPingService;
        private readonly H2MCommunicationService _h2MCommunicationService;
        private readonly H2MLauncherService _h2MLauncherService;
        private CancellationTokenSource _loadCancellation = new();
        private ServerViewModel? _serverViewModel;
        private int _totalServers = 0;
        private int _totalPlayers = 0;
        private string _filter = "";
        private string _updateStatus = "UpToDate";
        private string _updateColor;

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public IAsyncRelayCommand CheckUpdateStatusCommand { get; }
        public IRelayCommand JoinServerCommand { get; }
        public IRelayCommand LaunchH2MCommand { get; }
        public ObservableCollection<ServerViewModel> Servers { get; set; } = [];
        public string Filter
        {
            get => _filter;
            set => SetProperty(ref _filter, value);
        }
        public int TotalServers
        {
            get => _totalServers;
            private set => SetProperty(ref _totalServers, value);
        }
        public int TotalPlayers
        {
            get => _totalPlayers;
            private set => SetProperty(ref _totalPlayers, value);
        }
        public ServerViewModel SelectedServer
        {
            get => _serverViewModel;
            set => SetProperty(ref _serverViewModel, value);
        }
        public string UpdateStatus 
        { 
            get => _updateStatus;
            set {
                SetProperty(ref _updateStatus, value);
                UpdateColor = _updateStatus == "UpToDate" ? "DarkGreen" : "DarkRed";
            }  
        }
        public string UpdateColor 
        { 
            get => _updateColor;
            set => SetProperty(ref _updateColor, value); 
        }

        public ServerBrowserViewModel(
            RaidMaxService raidMaxService, 
            GameServerCommunicationService serverPingService,
            H2MCommunicationService h2MCommunicationService,
            H2MLauncherService h2MLauncherService)
        {
            _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));
            _serverPingService = serverPingService ?? throw new ArgumentNullException(nameof(serverPingService));
            _h2MCommunicationService = h2MCommunicationService ?? throw new ArgumentNullException(nameof(h2MCommunicationService));
            _h2MLauncherService = h2MLauncherService ?? throw new ArgumentNullException(nameof(h2MLauncherService));
            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
            JoinServerCommand = new RelayCommand(JoinServer);
            LaunchH2MCommand = new RelayCommand(LaunchH2M);
            CheckUpdateStatusCommand = new AsyncRelayCommand(CheckUpdateStatusAsync);
        }

        private async Task CheckUpdateStatusAsync()
        {
            if (await _h2MLauncherService.IsLauncherUpToDateAsync(CancellationToken.None))
                UpdateStatus = "UpToDate";
            else
                UpdateStatus = "New version available!";
        }

        private async Task LoadServersAsync()
        {
            await _loadCancellation.CancelAsync();
            _loadCancellation = new();

            try
            {
                Servers.Clear();
                TotalServers = 0;
                TotalPlayers = 0;

                // Get servers from the master
                var servers = await _raidMaxService.GetServerInfosAsync(_loadCancellation.Token);

                // Start by sending info requests to the game servers
                await _serverPingService.StartRetrievingGameServerInfo(servers, (server, gameServer) =>
                {
                    // Game server responded -> online
                    Servers.Add(new ServerViewModel()
                    {
                        Id = server.Id,
                        Ip = server.Ip,
                        Port = server.Port,
                        HostName = server.HostName,
                        ClientNum = server.ClientNum,
                        MaxClientNum = server.MaxClientNum,
                        Game = server.Game,
                        GameType = gameServer.GameType,
                        Map = gameServer.MapName,
                        Version = server.Version,
                        IsPrivate = gameServer.IsPrivate,
                        Ping = gameServer.Ping,
                        BotsNum = gameServer.Bots,
                    });

                    OnPropertyChanged(nameof(Servers));

                    TotalPlayers += server.ClientNum;
                    TotalServers++;
                }, _loadCancellation.Token);
            }
            catch (OperationCanceledException ex)
            {
                // canceled
                Debug.WriteLine($"LoadServersAsync cancelled: {ex.Message}");
            }
        }

        private void JoinServer()
        {
            if (SelectedServer is null)
                return;

            _h2MCommunicationService.JoinServer(SelectedServer.Ip, SelectedServer.Port.ToString());
        }

        private void LaunchH2M()
        {
            _h2MCommunicationService.LaunchH2MMod();
        }




    }
}
