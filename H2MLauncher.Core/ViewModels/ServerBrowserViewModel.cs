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

        private CancellationTokenSource _loadCancellation = new();

        [ObservableProperty]
        private ServerViewModel? _selectedServer;

        [ObservableProperty]
        private int _totalServers = 0;

        [ObservableProperty]
        private int _totalPlayers = 0;

        [ObservableProperty]
        private string _filter = "";

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public IRelayCommand JoinServerCommand { get; }
        public IRelayCommand LaunchH2MCommand { get; }
        public ObservableCollection<ServerViewModel> Servers { get; set; } = [];

        public ServerBrowserViewModel(
            RaidMaxService raidMaxService, 
            GameServerCommunicationService gameServerCommunicationService,
            H2MCommunicationService h2MCommunicationService)
        {
            _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));
            _gameServerCommunicationService = gameServerCommunicationService ?? throw new ArgumentNullException(nameof(gameServerCommunicationService));
            _h2MCommunicationService = h2MCommunicationService ?? throw new ArgumentNullException(nameof(h2MCommunicationService));

            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
            JoinServerCommand = new RelayCommand(JoinServer);
            LaunchH2MCommand = new RelayCommand(LaunchH2M);
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
