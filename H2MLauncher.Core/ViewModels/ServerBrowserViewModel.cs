using System.Collections.ObjectModel;
using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;

namespace H2MLauncher.Core.ViewModels
{
    public partial class ServerBrowserViewModel : ObservableObject
    {
        private readonly RaidMaxService _raidMaxService;
        private readonly GameServerCommunicationService _gameServerCommunicationService;
        private readonly H2MCommunicationService _h2MCommunicationService;
        private readonly H2MLauncherService _h2MLauncherService;
        private readonly IClipBoardService _clipBoardService;
        private CancellationTokenSource _loadCancellation = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(JoinServerCommand))]
        private ServerViewModel? _selectedServer;

        [ObservableProperty]
        private int _totalServers = 0;

        [ObservableProperty]
        private int _totalPlayers = 0;

        [ObservableProperty]
        private string _filter = "";
        
        [ObservableProperty]
        private string _updateStatus = "";
        
        public IAsyncRelayCommand RefreshServersCommand { get; }
        public IAsyncRelayCommand CheckUpdateStatusCommand { get; }
        public IRelayCommand JoinServerCommand { get; }
        public IRelayCommand LaunchH2MCommand { get; }
        public IRelayCommand CopyToClipBoardCommand { get; }
        public ObservableCollection<ServerViewModel> Servers { get; set; } = [];

        public ServerBrowserViewModel(
            RaidMaxService raidMaxService, 
            GameServerCommunicationService serverPingService,
            H2MCommunicationService h2MCommunicationService,
            GameServerCommunicationService gameServerCommunicationService,
            H2MLauncherService h2MLauncherService,
            IClipBoardService clipBoardService)
        {
            _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));
            _gameServerCommunicationService = gameServerCommunicationService ?? throw new ArgumentNullException(nameof(gameServerCommunicationService));
            _h2MCommunicationService = h2MCommunicationService ?? throw new ArgumentNullException(nameof(h2MCommunicationService));
            _h2MLauncherService = h2MLauncherService ?? throw new ArgumentNullException(nameof(h2MLauncherService));
            _clipBoardService = clipBoardService ?? throw new ArgumentNullException(nameof(clipBoardService)); ;
            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
            JoinServerCommand = new RelayCommand(JoinServer, () => _selectedServer is not null);
            LaunchH2MCommand = new RelayCommand(LaunchH2M);
            CheckUpdateStatusCommand = new AsyncRelayCommand(CheckUpdateStatusAsync);
            CopyToClipBoardCommand = new RelayCommand(DoCopyToClipBoardCommand);
        }

        private void DoCopyToClipBoardCommand()
        {
            if (SelectedServer is null)
                return;

            string textToCopy = $"connect {SelectedServer.Ip}:{SelectedServer.Port}";
            _clipBoardService.SaveToClipBoard(textToCopy);
        }

        private async Task CheckUpdateStatusAsync()
        {
            bool isUpToDate = await _h2MLauncherService.IsLauncherUpToDateAsync(CancellationToken.None);
            UpdateStatus = isUpToDate ? $"" : $"New version available: {_h2MLauncherService.LatestKnownVersion}!";
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
                await _gameServerCommunicationService.StartRetrievingGameServerInfo(servers, (server, gameServer) =>
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
