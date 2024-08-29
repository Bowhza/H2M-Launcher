using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Dialog.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Nogic.WritableOptions;

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
    private readonly IOptions<ResourceSettings> _resourceSettings;
    private readonly H2MLauncherSettings _defaultSettings;

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
    private IServerTabViewModel _selectedTab;

    public bool IsRecentsSelected => SelectedTab.TabName == RecentsTab.TabName;

    private ServerTabViewModel<ServerViewModel> AllServersTab { get; set; }
    private ServerTabViewModel<ServerViewModel> FavouritesTab { get; set; }
    private ServerTabViewModel<ServerViewModel> RecentsTab { get; set; }
    public ObservableCollection<IServerTabViewModel> ServerTabs { get; set; } = [];


    [ObservableProperty]
    private ServerFilterViewModel _advancedServerFilter;

    [ObservableProperty]
    private PasswordViewModel _passwordViewModel = new();
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

    public ObservableCollection<ServerViewModel> Servers { get; set; } = [];

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
        IOptions<ResourceSettings> resourceSettings,
        [FromKeyedServices(Constants.DefaultSettingsKey)] H2MLauncherSettings defaultSettings)
    {
        _raidMaxService = raidMaxService;
        _gameServerCommunicationService = gameServerCommunicationService;
        _h2MCommunicationService = h2MCommunicationService;
        _h2MLauncherService = h2MLauncherService;
        _clipBoardService = clipBoardService;
        _logger = logger;
        _saveFileService = saveFileService;
        _errorHandlingService = errorHandlingService;
        _dialogService = dialogService;
        _h2MLauncherOptions = h2mLauncherOptions;
        _defaultSettings = defaultSettings;
        _resourceSettings = resourceSettings;

        RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
        LaunchH2MCommand = new RelayCommand(LaunchH2M);
        CheckUpdateStatusCommand = new AsyncRelayCommand(CheckUpdateStatusAsync);
        CopyToClipBoardCommand = new RelayCommand<ServerViewModel>(DoCopyToClipBoardCommand);
        SaveServersCommand = new AsyncRelayCommand(SaveServersAsync);
        UpdateLauncherCommand = new AsyncRelayCommand(DoUpdateLauncherCommand, () => UpdateStatusText != "");
        OpenReleaseNotesCommand = new RelayCommand(DoOpenReleaseNotesCommand);
        RestartCommand = new RelayCommand(DoRestartCommand);
        ShowServerFilterCommand = new RelayCommand(ShowServerFilter);
        ShowSettingsCommand = new RelayCommand(ShowSettings);

        AdvancedServerFilter = new(_resourceSettings.Value, _defaultSettings.ServerFilter);

        if (!TryAddNewTab("All Servers", out ServerTabViewModel? allServersTab))
        {
            throw new Exception("Could not add all servers tab");
        }

        if (!TryAddNewTab("Favourites", out ServerTabViewModel? favouritesTab))
        {
            throw new Exception("Could not add favourites tab");
        }

        RecentsTab = new RecentServerTabViewModel(JoinServer, AdvancedServerFilter.ApplyFilter)
        {
            ToggleFavouriteCommand = new RelayCommand<ServerViewModel>(ToggleFavorite)
        };

        if (!TryAddNewTab(RecentsTab))
        {
            throw new Exception("Could not add recents tab");
        }

        AllServersTab = allServersTab;
        FavouritesTab = favouritesTab;

        SelectedTab = ServerTabs.First();

        foreach (IW4MObjectMap oMap in resourceSettings.Value.MapPacks.SelectMany(mappack => mappack.Maps))
        {
            _mapMap!.TryAdd(oMap.Name, oMap.Alias);
        }

        foreach (IW4MObjectMap oMap in resourceSettings.Value.GameTypes)
        {
            _gameTypeMap!.TryAdd(oMap.Name, oMap.Alias);
        }

        _h2MLauncherOptions.OnChange((newSettings, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // reset filter to stored values
                AdvancedServerFilter.ResetViewModel(newSettings.ServerFilter);
            });            
        });

        // initialize server filter view model with stored values
        AdvancedServerFilter.ResetViewModel(_h2MLauncherOptions.CurrentValue.ServerFilter);
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

            // save to settings
            _h2MLauncherOptions.Update(_h2MLauncherOptions.CurrentValue with
            {
                ServerFilter = AdvancedServerFilter.ToSettings()
            });
        }
    }

    private void OnServerFilterClosed(object? sender, RequestCloseEventArgs e)
    {
        if (e.DialogResult == true)
        {
            StatusText = "Server filter applied.";
        }
    }

    private bool TryAddNewTab<TServerViewModel>(IServerTabViewModel<TServerViewModel> tabViewModel)
        where TServerViewModel : ServerViewModel
    {
        if (ServerTabs.Any(tab => tab.TabName.Equals(tabViewModel.TabName, StringComparison.Ordinal)))
        {
            return false;
        }

        ServerTabs.Add(tabViewModel);
        return true;
    }

    private bool TryAddNewTab(string tabName, [MaybeNullWhen(false)] out ServerTabViewModel tabViewModel)
    {
        if (ServerTabs.Any(tab => tab.TabName.Equals(tabName, StringComparison.Ordinal)))
        {
            tabViewModel = null;
            return false;
        }

        tabViewModel = new ServerTabViewModel(tabName, JoinServer, AdvancedServerFilter.ApplyFilter)
        {
            ToggleFavouriteCommand = new RelayCommand<ServerViewModel>(ToggleFavorite),
        };

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
    public void AddOrUpdateRecentServerInSettings(RecentServerInfo recent)
    {
        List<RecentServerInfo> recents = GetRecentsFromSettings();

        int recentLimit = 30;

        // Remove existing servers with the same IP and port
        int removed = recents.RemoveAll(s => s.ServerIp == recent.ServerIp && s.ServerPort == recent.ServerPort);

        // Add the server with the updated date to the start of the list.
        // If the list exceeds the max size, remove the oldest entries (which are now at the end)
        recents = [recent, .. recents.OrderByDescending(r => r.Joined).Take(recentLimit - 1)]; ;

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
        _h2MLauncherOptions.Update(_h2MLauncherOptions.CurrentValue with
        {
            FavouriteServers = favorites
        }, true);
    }

    // Private method to save the list of recents to the settings.
    private void SaveRecents(List<RecentServerInfo> recents)
    {
        _h2MLauncherOptions.Update(settings =>
        {
            return settings with { RecentServers = recents };
        }, true);
    }

    private void ToggleFavorite(ServerViewModel? server)
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
                FavouritesTab.Servers.Add(server);
            }

            return;
        }

        // Remove from favorites
        RemoveFavoriteFromSettings(server.Ip, server.Port);

        // Remove from FavoriteServers collection
        FavouritesTab.Servers.Remove(server);
    }

    private void UpdateRecentJoinTime(ServerViewModel? server, DateTime joinedTime)
    {
        if (server is null)
            return;

        server.Joined = joinedTime;

        // Update in settings
        AddOrUpdateRecentServerInSettings(new RecentServerInfo
        {
            ServerIp = server.Ip,
            ServerName = server.HostName,
            ServerPort = server.Port,
            Joined = joinedTime
        });

        // Add to RecentServers collection if not already added
        if (!RecentsTab.Servers.Any(s => s.Ip == server.Ip && s.Port == server.Port))
        {
            RecentsTab.Servers.Add(server);
        }

        return;
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

    private void DoCopyToClipBoardCommand(ServerViewModel? server)
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

    public bool ServerFilter(ServerViewModel server)
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
                string? gameDirectory = Path.GetDirectoryName(_h2MLauncherOptions.Value.MWRLocation);

                directoryPath = Path.Combine(gameDirectory ?? "", directoryPath);
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
                RecentServerInfo? recentInfo = userRecents.FirstOrDefault(recent => recent.ServerIp == server.Ip && recent.ServerPort == server.Port);

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

                if (recentInfo is not null)
                {
                    serverViewModel.Joined = recentInfo.Joined;
                    RecentsTab.Servers.Add(serverViewModel);
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

    private void JoinServer(ServerViewModel? serverViewModel)
    {
        string? password = null;

        if (serverViewModel is null)
            return;

        if (serverViewModel.IsPrivate)
        {
            PasswordViewModel = new();

            bool? result = _dialogService.OpenDialog<PasswordDialog>(PasswordViewModel);

            password = PasswordViewModel.Password;

            // Do not continue joining the server
            if (result is null || result == false)
                return;
        } 
        
        bool hasJoined = _h2MCommunicationService.JoinServer(serverViewModel.Ip, serverViewModel.Port.ToString(), password);
        if (hasJoined)
        {
            UpdateRecentJoinTime(serverViewModel, DateTime.Now);
        }

        StatusText = hasJoined
            ? $"Joined {serverViewModel.Ip}:{serverViewModel.Port}"
            : "Ready";
    }

    private void LaunchH2M()
    {
        _h2MCommunicationService.LaunchH2MMod();
    }
}
