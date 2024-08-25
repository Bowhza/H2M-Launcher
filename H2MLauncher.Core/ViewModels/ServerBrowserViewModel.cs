using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.ViewModels
{
    public partial class ServerBrowserViewModel : ObservableObject
    {
        private readonly IH2MServersService _h2mServersService;
        private readonly GameServerCommunicationService<IW4MServer> _gameServerCommunicationService;
        private readonly H2MCommunicationService _h2MCommunicationService;
        private readonly H2MLauncherService _h2MLauncherService;
        private readonly IClipBoardService _clipBoardService;
        private readonly ISaveFileService _saveFileService;
        private readonly IErrorHandlingService _errorHandlingService;
        private CancellationTokenSource _loadCancellation = new();
        private readonly ILogger<ServerBrowserViewModel> _logger;
        private readonly MatchmakingService _matchmakingService;

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
        private string _updateStatusText = "";

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private double _updateDownloadProgress = 0;

        [ObservableProperty]
        private bool _progressBarVisibility = false;

        [ObservableProperty]
        private bool _releaseNotesVisibility = false;

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public IAsyncRelayCommand CheckUpdateStatusCommand { get; }
        public IRelayCommand JoinServerCommand { get; }
        public IRelayCommand LaunchH2MCommand { get; }
        public IRelayCommand CopyToClipBoardCommand { get; }
        public IRelayCommand SaveServersCommand { get; }
        public IAsyncRelayCommand UpdateLauncherCommand { get; }
        public IRelayCommand OpenReleaseNotesCommand { get; }
        public IRelayCommand RestartCommand { get; }

        public ObservableCollection<ServerViewModel> Servers { get; set; } = [];

        public ServerBrowserViewModel(
            IH2MServersService h2mServersService,
            H2MCommunicationService h2MCommunicationService,
            GameServerCommunicationService<IW4MServer> gameServerCommunicationService,
            H2MLauncherService h2MLauncherService,
            IClipBoardService clipBoardService,
            ILogger<ServerBrowserViewModel> logger,
            ISaveFileService saveFileService,
            IErrorHandlingService errorHandlingService,
            MatchmakingService matchmakingService)
        {
            _h2mServersService = h2mServersService ?? throw new ArgumentNullException(nameof(h2mServersService));
            _gameServerCommunicationService = gameServerCommunicationService ?? throw new ArgumentNullException(nameof(gameServerCommunicationService));
            _h2MCommunicationService = h2MCommunicationService ?? throw new ArgumentNullException(nameof(h2MCommunicationService));
            _h2MLauncherService = h2MLauncherService ?? throw new ArgumentNullException(nameof(h2MLauncherService));
            _clipBoardService = clipBoardService ?? throw new ArgumentNullException(nameof(clipBoardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _saveFileService = saveFileService ?? throw new ArgumentNullException(nameof(saveFileService));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
            JoinServerCommand = new AsyncRelayCommand(JoinServer, () => _selectedServer is not null);
            LaunchH2MCommand = new RelayCommand(LaunchH2M);
            CheckUpdateStatusCommand = new AsyncRelayCommand(CheckUpdateStatusAsync);
            CopyToClipBoardCommand = new RelayCommand<ServerViewModel>(DoCopyToClipBoardCommand);
            SaveServersCommand = new AsyncRelayCommand(SaveServersAsync);
            UpdateLauncherCommand = new AsyncRelayCommand(DoUpdateLauncherCommand, () => UpdateStatusText != "");
            OpenReleaseNotesCommand = new RelayCommand(DoOpenReleaseNotesCommand);
            RestartCommand = new RelayCommand(DoRestartCommand);

            _gameServerCommunicationService.ServerInfoReceived += OnGameServerInfoReceived;
            _matchmakingService = matchmakingService;
            _matchmakingService.StartConnection();
        }

        private void DoRestartCommand()
        {
            Process.Start(H2MLauncherService.LauncherPath);
            Process.GetCurrentProcess().Kill();
        }

        private void DoOpenReleaseNotesCommand()
        {
            string destinationurl = "https://github.com/Bowhza/H2M-Launcher/releases/latest";
            ProcessStartInfo sInfo = new(destinationurl)
            {
                UseShellExecute = true,
            };
            Process.Start(sInfo);
        }

        private async Task DoUpdateLauncherCommand()
        {
            ProgressBarVisibility = true;
            await _h2MLauncherService.UpdateLauncherToLatestVersion((double progress) =>
            {
                UpdateDownloadProgress = progress;
                if (progress == 100)
                {
                    ProgressBarVisibility = false;
                    ReleaseNotesVisibility = true;
                }
            }, CancellationToken.None).ConfigureAwait(false);
        }

        private void DoCopyToClipBoardCommand(ServerViewModel? server)
        {
            if (server is null)
            {
                if (SelectedServer is null)
                    return;

                server = SelectedServer;
            }

            string textToCopy = $"connect {server.Ip}:{server.Port}";
            _clipBoardService.SaveToClipBoard(textToCopy);

            StatusText = $"Copied to clipboard";
        }

        public bool ServerFilter(ServerViewModel server)
        {
            if (string.IsNullOrEmpty(Filter))
                return true;

            string lowerCaseFilter = Filter.ToLower();

            return server.ToString().Contains(lowerCaseFilter, StringComparison.OrdinalIgnoreCase);
        }

        private async Task SaveServersAsync()
        {
            // Create a list of "Ip:Port" strings
            List<string> ipPortList = Servers.Where(ServerFilter)
                                             .Select(server => $"{server.Ip}:{server.Port}")
                                             .ToList();

            // Serialize the list into JSON format
            string jsonString = JsonSerializer.Serialize<List<string>>(ipPortList);

            try
            {
                // Store the server list into the corresponding directory
                _logger.LogDebug("Storing server list into \"/players2/favourites.json\"");

                string fileName = "./players2/favourites.json";

                if (!Directory.Exists("./players2"))
                {
                    // let user choose
                    fileName = await _saveFileService.SaveFileAs("favourites.json", "JSON file (*.json)|*.json") ?? "";
                    if (string.IsNullOrEmpty(fileName))
                        return;
                }

                await File.WriteAllTextAsync(fileName, jsonString);

                _logger.LogInformation("Stored server list into {fileName}", fileName);

                StatusText = $"{ipPortList.Count} servers saved to {Path.GetFileName(fileName)}";
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Could not save favourites.json file. Make sure the exe is inside the root of the game folder.");
            }
        }

        private async Task CheckUpdateStatusAsync()
        {
            bool isUpToDate = await _h2MLauncherService.IsLauncherUpToDateAsync(CancellationToken.None);
            UpdateStatusText = isUpToDate ? $"" : $"New version available: {_h2MLauncherService.LatestKnownVersion}!";
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
                IEnumerable<IW4MServer> servers = await _h2mServersService.GetServersAsync(_loadCancellation.Token);

                // Start by sending info requests to the game servers
                await _gameServerCommunicationService.SendInfoRequestsAsync(servers, _loadCancellation.Token);
            }
            catch (OperationCanceledException ex)
            {
                // canceled
                Debug.WriteLine($"LoadServersAsync cancelled: {ex.Message}");
            }
        }


        private readonly Dictionary<long, IW4MServer> _queuedServers = [];
        private Task? _queueTask;
        private CancellationTokenSource _queueCancellation = new();

        private void QueueServer(IW4MServer server)
        {
            _queuedServers.Add(server.Id, server);

            if (_queueTask is null || _queueTask.IsCompleted)
            {
                _queueCancellation = new();
                _queueTask = ProcessServerQueueAsync(_queueCancellation.Token);
            }
        }

        private async Task ProcessServerQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _queuedServers.Count > 0)
            {
                await _gameServerCommunicationService.SendInfoRequestsAsync(_queuedServers.Values, cancellationToken);

                // Delay before sending the next request (to avoid spamming)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        private void StopServerQueue()
        {
            _queueCancellation.Cancel();
            _queueTask?.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Queue task faulted:");
                }
            });

            _queueTask = null;
        }

        private void CheckQueue(long serverId, GameServerInfo newServerInfo)
        {
            if (_queuedServers.Count == 0)
            {
                // early return
                return;
            }

            if (!_queuedServers.TryGetValue(serverId, out var server))
            {
                return;
            }

            if (newServerInfo.MaxClients - (newServerInfo.Clients - newServerInfo.Bots) > 0)
            {
                // we can try to join
                bool joined = _h2MCommunicationService.JoinServer(server.Ip, server.Port.ToString());

                StatusText = joined
                    ? $"Joined {server.Ip}:{server.Port}"
                    : "Ready";

                if (joined)
                {
                    StopServerQueue();
                }
            }
        }

        private readonly object _serverLockObj = new();

        private class ServerViewModelComparer : IEqualityComparer<ServerViewModel>
        {
            public bool Equals(ServerViewModel? x, ServerViewModel? y)
            {
                if (x == y)
                {
                    return true;
                }

                if (x is null && y is null)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                if (x.Ip != y.Ip)
                {
                    return false;
                }

                return x.Port == y.Port;
            }

            public int GetHashCode([DisallowNull] ServerViewModel obj)
            {
                return HashCode.Combine(obj.Ip, obj.Port);
            }
        }

        private void OnGameServerInfoReceived(object? sender, ServerInfoEventArgs<IW4MServer> e)
        {
            var server = e.Server;
            var gameServer = e.ServerInfo;

            lock (_serverLockObj)
            {

                // Game server responded -> online
                bool added = Servers.AddOrUpdate(
                    new ServerViewModel(server)
                    {
                        Id = server.Id,
                        Ip = server.Ip,
                        Port = server.Port,
                        HostName = server.HostName,
                        ClientNum = gameServer.Clients - gameServer.Bots,
                        MaxClientNum = gameServer.MaxClients,
                        Game = server.Game,
                        GameType = gameServer.GameType,
                        Map = gameServer.MapName,
                        Version = server.Version,
                        IsPrivate = gameServer.IsPrivate,
                        Ping = gameServer.Ping,
                        BotsNum = gameServer.Bots,
                    },

                    // update on id
                    new ServerViewModelComparer()
                );

                if (added)
                {
                    TotalPlayers += server.ClientNum;
                    TotalServers++;
                }
                else
                {
                    TotalPlayers = Servers.Sum(s => s.ClientNum);
                    TotalServers = Servers.Count;
                }
            }

            CheckQueue(server.Id, gameServer);
        }

        private async Task JoinServer()
        {
            if (SelectedServer is null)
                return;

            if (SelectedServer.ClientNum >= SelectedServer.MaxClientNum)
            {
                //QueueServer(SelectedServer.Server);
                await _matchmakingService.JoinQueueAsync(SelectedServer.Server, "IchWillKeinEbola");
                return;
            }

            StatusText = _h2MCommunicationService.JoinServer(SelectedServer.Ip, SelectedServer.Port.ToString())
                ? $"Joined {SelectedServer.Ip}:{SelectedServer.Port}"
                : "Ready";
        }

        private void LaunchH2M()
        {
            _h2MCommunicationService.LaunchH2MMod();
        }
    }
}
