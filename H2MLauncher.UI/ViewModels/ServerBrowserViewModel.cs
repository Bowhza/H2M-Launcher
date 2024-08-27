using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

using Awesome.Net.WritableOptions;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Dialog.Views;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.UI.ViewModels;

public partial class ServerBrowserViewModel : ObservableObject
{
    private readonly RaidMaxService _raidMaxService;
    private readonly GameServerCommunicationService _gameServerCommunicationService;
    private readonly H2MCommunicationService _h2MCommunicationService;
    private readonly H2MLauncherService _h2MLauncherService;
    private readonly IClipBoardService _clipBoardService;
    private readonly ISaveFileService _saveFileService;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly ILogger<ServerBrowserViewModel> _logger;
    private readonly IWritableOptions<H2MLauncherSettings> _h2MLauncherOptions;
    private readonly DialogService _dialogService;
    private CancellationTokenSource _loadCancellation = new();
    private readonly Dictionary<string, string> _mapMap = [];
    private readonly Dictionary<string, string> _gameTypeMap = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateLauncherCommand))]
    private string _updateStatusText = "";

    [ObservableProperty]
    private double _updateDownloadProgress = 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateLauncherCommand))]
    private bool _updateFinished;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _filter = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecentsSelected))]
    private ServerTabViewModel<IServerViewModel> _selectedTab;

    public bool IsRecentsSelected => SelectedTab.TabName == RecentsTab.TabName;

    private ServerTabViewModel<IServerViewModel> AllServersTab { get; set; }
    private ServerTabViewModel<IServerViewModel> FavouritesTab { get; set; }
    private ServerTabViewModel<IServerViewModel> RecentsTab { get; set; }
    public ObservableCollection<ServerTabViewModel<IServerViewModel>> ServerTabs { get; set; } = [];

    [ObservableProperty]
    private ServerFilterViewModel _advancedServerFilter;

    public event Action? ServerFilterChanged;

    public IAsyncRelayCommand RefreshServersCommand { get; }
    public IAsyncRelayCommand CheckUpdateStatusCommand { get; }
    public IRelayCommand LaunchH2MCommand { get; }
    public IRelayCommand CopyToClipBoardCommand { get; }
    public IRelayCommand SaveServersCommand { get; }
    public IAsyncRelayCommand UpdateLauncherCommand { get; }
    public IRelayCommand OpenReleaseNotesCommand { get; }
    public IRelayCommand RestartCommand { get; }
    public IRelayCommand ShowServerFilterCommand { get; }
    public IRelayCommand ShowSettingsCommand { get; }

    public ObservableCollection<IServerViewModel> Servers { get; set; } = [];

    public ServerBrowserViewModel(
        RaidMaxService raidMaxService,
        H2MCommunicationService h2MCommunicationService,
        GameServerCommunicationService gameServerCommunicationService,
        H2MLauncherService h2MLauncherService,
        IClipBoardService clipBoardService,
        ILogger<ServerBrowserViewModel> logger,
        ISaveFileService saveFileService,
        IErrorHandlingService errorHandlingService,
        DialogService dialogService,
        IWritableOptions<H2MLauncherSettings> h2mLauncherOptions,
        IOptions<ResourceSettings> resoureceOptions)
    {
        _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));
        _gameServerCommunicationService = gameServerCommunicationService ?? throw new ArgumentNullException(nameof(gameServerCommunicationService));
        _h2MCommunicationService = h2MCommunicationService ?? throw new ArgumentNullException(nameof(h2MCommunicationService));
        _h2MLauncherService = h2MLauncherService ?? throw new ArgumentNullException(nameof(h2MLauncherService));
        _clipBoardService = clipBoardService ?? throw new ArgumentNullException(nameof(clipBoardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _saveFileService = saveFileService ?? throw new ArgumentNullException(nameof(saveFileService));
        _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        _dialogService = dialogService;
        ArgumentNullException.ThrowIfNull(h2mLauncherOptions);
        _h2MLauncherOptions = h2mLauncherOptions;
        ArgumentNullException.ThrowIfNull(resoureceOptions);

        _advancedServerFilter = new(resoureceOptions.Value);

        RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
        LaunchH2MCommand = new RelayCommand(LaunchH2M);
        CheckUpdateStatusCommand = new AsyncRelayCommand(CheckUpdateStatusAsync);
        CopyToClipBoardCommand = new RelayCommand<IServerViewModel>(DoCopyToClipBoardCommand);
        SaveServersCommand = new AsyncRelayCommand(SaveServersAsync);
        UpdateLauncherCommand = new AsyncRelayCommand(DoUpdateLauncherCommand, () => UpdateStatusText != "");
        OpenReleaseNotesCommand = new RelayCommand(DoOpenReleaseNotesCommand);
        RestartCommand = new RelayCommand(DoRestartCommand);
        ShowServerFilterCommand = new RelayCommand(ShowServerFilter);
        ShowSettingsCommand = new RelayCommand(ShowSettings);

        ServerTabViewModel<IServerViewModel> allServersTab = new ServerTabViewModel<ServerViewModel>("All Servers", JoinServer)
        {
            ToggleFavouriteCommand = new RelayCommand<IServerViewModel>(ToggleFavorite),
        };
        AllServersTab = TryAddNewTab(allServersTab) 
            ? allServersTab 
            : throw new Exception("Could not add all servers tab");

        ServerTabViewModel<IServerViewModel> favouritesTab = new ServerTabViewModel<ServerViewModel>("Favourites", JoinServer)
        {
            ToggleFavouriteCommand = new RelayCommand<IServerViewModel>(ToggleFavorite),
        };
        FavouritesTab = TryAddNewTab(favouritesTab)
            ? favouritesTab
            : throw new Exception("Could not add favourites tab");

        ServerTabViewModel<IServerViewModel> recentsTab = new ServerTabViewModel<RecentServerViewModel>("Recents", JoinServer)
        {
            ToggleFavouriteCommand = new RelayCommand<IServerViewModel>(ToggleFavorite),
        };
        RecentsTab = TryAddNewTab(recentsTab)
            ? recentsTab
            : throw new Exception("Could not add recents tab");

        foreach (IW4MObjectMap oMap in resoureceOptions.Value.MapPacks.SelectMany(mappack => mappack.Maps))
        {
            _mapMap!.TryAdd(oMap.Name, oMap.Alias);
        }

        foreach (IW4MObjectMap oMap in resoureceOptions.Value.GameTypes)
        {
            _gameTypeMap!.TryAdd(oMap.Name, oMap.Alias);
        }
        
        SelectedTab = ServerTabs.First();
    }

    private void ShowSettings()
    {
        SettingsViewModel settingsViewModel = new(_h2MLauncherOptions);

        if (_dialogService.OpenDialog<SettingsDialogView>(settingsViewModel) == true)
        {
            // settings saved;
        }
    }

    private void ShowServerFilter()
    {
        if (_dialogService.OpenDialog<FilterDialogView>(AdvancedServerFilter) == true)
        {
            OnPropertyChanged(nameof(Servers));
            ServerFilterChanged?.Invoke();
            StatusText = "Server filter applied.";
        }
    }

    private void OnServerFilterClosed(object? sender, RequestCloseEventArgs e)
    {
        if (e.DialogResult == true)
        {
            StatusText = "Server filter applied.";
        }
    }

    private bool TryAddNewTab(ServerTabViewModel<IServerViewModel> tabViewModel)
    {
        if (ServerTabs.Any(tab => tab.TabName.Equals(tabViewModel.TabName, StringComparison.Ordinal)))
        {
            return false;
        }

        ServerTabs.Add(tabViewModel);
        return true;
    }

    // Method to get the user's favorites from the settings.
    public List<SimpleServerInfo> GetFavoritesFromSettings()
    {
        return _h2MLauncherOptions.Value.FavouriteServers;
    }

    // Method to get user's recent servers from settings.
    public List<RecentServerInfo> GetRecentsFromSettings()
    {
        return _h2MLauncherOptions.Value.RecentServers;
    }

    // Method to add a favorite to the settings.
    public void AddFavoriteToSettings(SimpleServerInfo favorite)
    {
        List<SimpleServerInfo> favorites = GetFavoritesFromSettings();

        // Add the new favorite to the list.
        favorites.Add(favorite);

        // Save the updated list to the settings.
        SaveFavorites(favorites);
    }

    // Method to add a recent to the settings.
    public void AddRecentToSettings(RecentServerInfo recent)
    {
        List<RecentServerInfo> recents = GetRecentsFromSettings();

        // Add the new recent to the list.
        recents.Add(recent);

        // Sort recents by Joined DateTime
        if (recents.Count > 50)
        {
            recents = [.. recents.OrderBy(r => r.Joined).Take(50)];
        }

        // Save the updated list to the settings.
        SaveRecents(recents);
    }

    // Method to remove a favorite from the settings.
    public void RemoveFavoriteFromSettings(string serverIp, int serverPort)
    {
        List<SimpleServerInfo> favorites = GetFavoritesFromSettings();

        // Remove the favorite that matches the provided ServerIp.
        favorites.RemoveAll(fav => fav.ServerIp == serverIp && fav.ServerPort == serverPort);

        // Save the updated list to the settings.
        SaveFavorites(favorites);
    }

    // Private method to save the list of favorites to the settings.
    private void SaveFavorites(List<SimpleServerInfo> favorites)
    {
        _h2MLauncherOptions.Update(settings =>
        {
            settings.FavouriteServers = favorites;
        }, true);
    }

    // Private method to save the list of recents to the settings.
    private void SaveRecents(List<RecentServerInfo> recents)
    {
        _h2MLauncherOptions.Update(settings =>
        {
            settings.RecentServers = recents;
        }, true);
    }

    private void ToggleFavorite(IServerViewModel? server)
    {
        if (server is null)
            return;

        server.IsFavorite = !server.IsFavorite;

        if (server.IsFavorite)
        {
            // Add to favorites
            AddFavoriteToSettings(new SimpleServerInfo
            {
                ServerIp = server.Ip,
                ServerName = server.HostName,
                ServerPort = server.Port
            });

            // Add to FavoriteServers collection if not already added
            if (!FavouritesTab.Servers.Any(s => s.Ip == server.Ip && s.Port == server.Port))
            {
                FavouritesTab.Servers.Add((ServerViewModel)server);
            }

            return;
        }

        // Remove from favorites
        RemoveFavoriteFromSettings(server.Ip, server.Port);

        // Remove from FavoriteServers collection
        FavouritesTab.Servers.Remove((ServerViewModel)server);
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

    private Task<bool> DoUpdateLauncherCommand()
    {
        return _h2MLauncherService.UpdateLauncherToLatestVersion((double progress) =>
        {
            UpdateDownloadProgress = progress;
            if (progress == 100)
            {
                UpdateFinished = true;
            }
        }, CancellationToken.None);
    }

    private void DoCopyToClipBoardCommand(IServerViewModel? server)
    {
        if (server is null)
        {
            if (SelectedTab.SelectedServer is null)
                return;

            server = SelectedTab.SelectedServer;
        }

        string textToCopy = $"connect {server.Ip}:{server.Port}";
        _clipBoardService.SaveToClipBoard(textToCopy);

        StatusText = $"Copied to clipboard";
    }

    public bool ServerFilter(IServerViewModel server)
    {
        return AdvancedServerFilter.ApplyFilter(server);
    }

    private async Task SaveServersAsync()
    {
        // Create a list of "Ip:Port" strings
        List<string> ipPortList = SelectedTab.Servers.Where(ServerFilter)
                                         .Select(server => $"{server.Ip}:{server.Port}")
                                         .ToList();

        // Serialize the list into JSON format
        string jsonString = JsonSerializer.Serialize(ipPortList, JsonContext.Default.ListString);

        try
        {
            // Store the server list into the corresponding directory
            _logger.LogDebug("Storing server list into \"/players2/favourites.json\"");

            string directoryPath = "players2";
            if (!string.IsNullOrEmpty(_h2MLauncherOptions.Value.MWRLocation))
            {
                directoryPath = Path.Combine(_h2MLauncherOptions.Value.MWRLocation, directoryPath);
            }

            string fileName;

            if (!Directory.Exists(directoryPath))
            {
                // let user choose
                fileName = await _saveFileService.SaveFileAs("favourites.json", "JSON file (*.json)|*.json") ?? "";
                if (string.IsNullOrEmpty(fileName))
                    return;
            }
            else
            {
                fileName = Path.Combine(directoryPath, "favourites.json");
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
            StatusText = "Refreshing servers...";

            AllServersTab.Servers.Clear();
            FavouritesTab.Servers.Clear();
            RecentsTab.Servers.Clear();

            // Get servers from the master
            List<RaidMaxServer> servers = await _raidMaxService.GetServerInfosAsync(_loadCancellation.Token);

            List<SimpleServerInfo> userFavorites = GetFavoritesFromSettings();
            List<RecentServerInfo> userRecents = GetRecentsFromSettings();

            // Let's prioritize populated servers first for getting game server info.
            IEnumerable<RaidMaxServer> serversOrderedByOccupation = servers
                .OrderByDescending((server) => server.ClientNum);

            // Start by sending info requests to the game servers
            await Task.Run(() => _gameServerCommunicationService.StartRetrievingGameServerInfo(serversOrderedByOccupation, (server, gameServer) =>
            {
                bool isFavorite = userFavorites.Any(fav => fav.ServerIp == server.Ip && fav.ServerPort == server.Port);
                List<RecentServerInfo> recents = userRecents.Where(recent => recent.ServerIp == server.Ip && recent.ServerPort == server.Port).ToList();

                _mapMap.TryGetValue(gameServer.MapName, out string? mapDisplayName);
                _gameTypeMap.TryGetValue(gameServer.GameType, out string? gameTypeDisplayName);
                
                ServerViewModel serverViewModel = new()
                {
                    Id = server.Id,
                    Ip = server.Ip,
                    Port = server.Port,
                    HostName = server.HostName,
                    ClientNum = gameServer.Clients - gameServer.Bots,
                    MaxClientNum = gameServer.MaxClients,
                    Game = server.Game,
                    GameType = gameServer.GameType,
                    GameTypeDisplayName = gameTypeDisplayName ?? gameServer.GameType,
                    Map = gameServer.MapName,
                    MapDisplayName = mapDisplayName ?? gameServer.MapName,
                    Version = server.Version,
                    IsPrivate = gameServer.IsPrivate,
                    Ping = gameServer.Ping,
                    BotsNum = gameServer.Bots,
                    IsFavorite = isFavorite
                };

                // Game server responded -> online
                AllServersTab.Servers.Add(serverViewModel);

                if (isFavorite)
                {
                    FavouritesTab.Servers.Add(serverViewModel);
                }

                if (recents.Count != 0)
                {
                    foreach (RecentServerInfo rec in recents)
                    {
                        RecentServerViewModel recentServerViewModel = new() 
                        {
                            Id = serverViewModel.Id,
                            Ip = serverViewModel.Ip,
                            Port = serverViewModel.Port,
                            HostName = serverViewModel.HostName,
                            ClientNum = serverViewModel.ClientNum,
                            MaxClientNum = serverViewModel.MaxClientNum,
                            Game = serverViewModel.Game,
                            GameType = serverViewModel.GameType,
                            GameTypeDisplayName = serverViewModel.GameTypeDisplayName,
                            Map = serverViewModel.Map,
                            MapDisplayName = serverViewModel.MapDisplayName,
                            Version = serverViewModel.Version,
                            IsPrivate = serverViewModel.IsPrivate,
                            Ping = serverViewModel.Ping,
                            BotsNum = serverViewModel.BotsNum,
                            IsFavorite = serverViewModel.IsFavorite,
                            Joined = rec.Joined
                        };
                        RecentsTab.Servers.Add(recentServerViewModel);
                    }
                }
                // Game server responded -> online

            }, _loadCancellation.Token));
            StatusText = "Ready";
        }
        catch (OperationCanceledException ex)
        {
            // canceled
            Debug.WriteLine($"LoadServersAsync cancelled: {ex.Message}");
        }
    }

    private void JoinServer(IServerViewModel? serverViewModel)
    {
        if (serverViewModel is null)
            return;

        bool hasJoined = _h2MCommunicationService.JoinServer(serverViewModel.Ip, serverViewModel.Port.ToString());
        
        AddRecentToSettings(new RecentServerInfo()
        {
            Joined = DateTime.Now,
            ServerIp = serverViewModel.Ip,
            ServerName = serverViewModel.HostName,
            ServerPort = serverViewModel.Port
        });

        StatusText = hasJoined
            ? $"Joined {serverViewModel.Ip}:{serverViewModel.Port}"
            : "Ready";

        RefreshServersCommand.Execute(this);
    }

    private void LaunchH2M()
    {
        _h2MCommunicationService.LaunchH2MMod();
    }
}
