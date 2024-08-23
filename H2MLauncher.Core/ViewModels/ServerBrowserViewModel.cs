using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.ViewModels
{
    public partial class ServerBrowserViewModel : ObservableObject
    {
        private readonly RaidMaxService _raidMaxService;
        private readonly GameServerCommunicationService _gameServerCommunicationService;
        private readonly H2MCommunicationService _h2MCommunicationService;
        private readonly H2MLauncherService _h2MLauncherService;
        private readonly IClipBoardService _clipBoardService;
        private readonly ISaveFileService _saveFileService;
        private readonly IErrorHandlingService _errorHandlingService;
        private CancellationTokenSource _loadCancellation = new();
        private readonly ILogger<ServerBrowserViewModel> _logger;

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

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private double _updateDownloadProgress = 0;

        [ObservableProperty]
        private string _progressBarVisibility = "Hidden";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateLauncherCommand))]
        private bool _autoUpdateFinished = false;

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public IAsyncRelayCommand CheckUpdateStatusCommand { get; }
        public IRelayCommand JoinServerCommand { get; }
        public IRelayCommand LaunchH2MCommand { get; }
        public IRelayCommand CopyToClipBoardCommand { get; }
        public IRelayCommand SaveServersCommand { get; }
        public IAsyncRelayCommand UpdateLauncherCommand { get; }

        public ObservableCollection<ServerViewModel> Servers { get; set; } = [];

        public ServerBrowserViewModel(
            RaidMaxService raidMaxService,
            H2MCommunicationService h2MCommunicationService,
            GameServerCommunicationService gameServerCommunicationService,
            H2MLauncherService h2MLauncherService,
            IClipBoardService clipBoardService,
            ILogger<ServerBrowserViewModel> logger,
            ISaveFileService saveFileService,
            IErrorHandlingService errorHandlingService)
        {
            _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));
            _gameServerCommunicationService = gameServerCommunicationService ?? throw new ArgumentNullException(nameof(gameServerCommunicationService));
            _h2MCommunicationService = h2MCommunicationService ?? throw new ArgumentNullException(nameof(h2MCommunicationService));
            _h2MLauncherService = h2MLauncherService ?? throw new ArgumentNullException(nameof(h2MLauncherService));
            _clipBoardService = clipBoardService ?? throw new ArgumentNullException(nameof(clipBoardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _saveFileService = saveFileService ?? throw new ArgumentNullException(nameof(saveFileService));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
            JoinServerCommand = new RelayCommand(JoinServer, () => _selectedServer is not null);
            LaunchH2MCommand = new RelayCommand(LaunchH2M);
            CheckUpdateStatusCommand = new AsyncRelayCommand(CheckUpdateStatusAsync);
            CopyToClipBoardCommand = new RelayCommand<ServerViewModel>(DoCopyToClipBoardCommand);
            SaveServersCommand = new AsyncRelayCommand(SaveServersAsync);
            UpdateLauncherCommand = new AsyncRelayCommand(DoUpdateLauncherCommand, () => _updateStatus != "" && !AutoUpdateFinished);
        }

        private async Task DoUpdateLauncherCommand()
        {
            ProgressBarVisibility = "Visible";
            await _h2MLauncherService.UpdateLauncherToLatestVersion((double progress) =>
            {
                UpdateDownloadProgress = progress;
                if (progress == 100)
                {
                    AutoUpdateFinished = true;
                    ProgressBarVisibility = "Hidden";
                    UpdateStatus = "Restart for new version!";
                }
            }, CancellationToken.None).ConfigureAwait(false);

            // NOTE: we could still show the release page with the newest features or in the APP?
            
            //string destinationurl = "https://github.com/Bowhza/H2M-Launcher/releases/latest";
            //ProcessStartInfo sInfo = new(destinationurl)
            //{
            //    UseShellExecute = true,
            //};
            //Process.Start(sInfo);
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
            string jsonString = JsonSerializer.Serialize(ipPortList, JsonContext.Default.ListString);

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
                        ClientNum = gameServer.Clients - gameServer.Bots,
                        MaxClientNum = gameServer.MaxClients,
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
