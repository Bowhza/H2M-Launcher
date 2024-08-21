using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace H2MLauncher.Core.ViewModels
{
    public class ServerBrowserViewModel : ObservableObject
    {
        private readonly RaidMaxService _raidMaxService;
        private readonly ServerPingService _serverPingService;
        private int _totalServers = 0;
        private int _totalPlayers = 0;

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public ObservableCollection<ServerViewModel> Servers { get; private set; } = [];

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

        public ServerBrowserViewModel(RaidMaxService raidMaxService, ServerPingService serverPingService)
        {
            _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));

            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
            _serverPingService = serverPingService;
        }

        private CancellationTokenSource _loadCancellation = new();

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

                    TotalPlayers += server.ClientNum;
                    TotalServers++;
                }, _loadCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // canceled
            }
        }
    }
}
