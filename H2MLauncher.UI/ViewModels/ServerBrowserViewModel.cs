using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core;
using H2MLauncher.Core.Game;
using H2MLauncher.Core.Game.Models;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Dialog.Views;
using H2MLauncher.UI.Messages;
using H2MLauncher.UI.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Nogic.WritableOptions;

namespace H2MLauncher.UI.ViewModels;

public partial class ServerBrowserViewModel : ObservableRecipient, IRecipient<SelectServerMessage>, IDisposable
{
    private readonly IMasterServerService _masterServerService;
    private readonly IGameServerInfoService<IServerConnectionDetails> _tcpGameServerCommunicationService;
    private readonly H2MCommunicationService _h2MCommunicationService;
    private readonly LauncherService _h2MLauncherService;
    private readonly IClipBoardService _clipBoardService;
    private readonly ISaveFileService _saveFileService;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly DialogService _dialogService;
    private readonly IMapsProvider _mapsProvider;
    private readonly ILogger<ServerBrowserViewModel> _logger;


    private readonly IServerJoinService _serverJoinService;

    private readonly IWritableOptions<H2MLauncherSettings> _h2MLauncherOptions;
    private readonly IOptions<ResourceSettings> _resourceSettings;

    private CancellationTokenSource _loadCancellation = new();
    private readonly MatchmakingService _matchmakingService;
    private readonly QueueingService _queueingService;
    private readonly IOnlineServices _onlineService;
    private readonly CachedServerDataService _serverDataService;
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

    [ObservableProperty]
    private GameStateViewModel _gameState = new();

    [ObservableProperty]
    private ServerFilterViewModel _advancedServerFilter;

    [ObservableProperty]
    private ShortcutsViewModel _shortcuts;

    [ObservableProperty]
    private PasswordViewModel _passwordViewModel = new();

    [ObservableProperty]
    private SocialsViewModel _socials = new();

    [ObservableProperty]
    private MatchmakingViewModel? _matchmakingViewModel;

    public SocialOverviewViewModel SocialOverviewViewModel { get; }

    public bool IsRecentsSelected => SelectedTab.TabName == RecentsTab.TabName;

    public bool IsMatchmakingEnabled =>
        _h2MCommunicationService.GameDetection.IsGameDetectionRunning &&
        _h2MLauncherOptions.CurrentValue.GameMemoryCommunication &&
        _h2MLauncherOptions.CurrentValue.ServerQueueing;

    private ServerTabViewModel<ServerViewModel> AllServersTab { get; set; }
    private ServerTabViewModel<ServerViewModel> HMWServersTab { get; set; }
    private ServerTabViewModel<ServerViewModel> FavouritesTab { get; set; }
    private ServerTabViewModel<ServerViewModel> RecentsTab { get; set; }
    public ObservableCollection<IServerTabViewModel> ServerTabs { get; set; } = [];


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
    public IAsyncRelayCommand ReconnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand EnterMatchmakingCommand { get; }

    public ServerBrowserViewModel(
        IMasterServerService masterServerService,
        [FromKeyedServices("TCP")] IGameServerInfoService<IServerConnectionDetails> tcpGameServerService,
        H2MCommunicationService h2MCommunicationService,
        LauncherService h2MLauncherService,
        IClipBoardService clipBoardService,
        ILogger<ServerBrowserViewModel> logger,
        ISaveFileService saveFileService,
        IErrorHandlingService errorHandlingService,
        DialogService dialogService,
        IWritableOptions<H2MLauncherSettings> h2mLauncherOptions,
        IOptions<ResourceSettings> resourceSettings,
        [FromKeyedServices(Constants.DefaultSettingsKey)] H2MLauncherSettings defaultSettings,
        QueueingService queueingService,
        MatchmakingService matchmakingService,
        CachedServerDataService serverDataService,
        IMapsProvider mapsProvider,
        IServerJoinService serverJoinService,
        IOnlineServices onlineService,
        SocialOverviewViewModel socialOverviewViewModel)
    {
        _masterServerService = masterServerService;
        _tcpGameServerCommunicationService = tcpGameServerService;
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
        _matchmakingService = matchmakingService;
        _queueingService = queueingService;
        _onlineService = onlineService;
        _serverDataService = serverDataService;
        _mapsProvider = mapsProvider;
        _serverJoinService = serverJoinService;

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
        ReconnectCommand = new AsyncRelayCommand(ReconnectServer);
        DisconnectCommand = new AsyncRelayCommand(DisconnectServer);
        EnterMatchmakingCommand = new AsyncRelayCommand(EnterMatchmaking, () => IsMatchmakingEnabled);

        SocialOverviewViewModel = socialOverviewViewModel;
        AdvancedServerFilter = new(_resourceSettings.Value, _defaultSettings.ServerFilter);
        Shortcuts = new();

        if (!TryAddNewTab("All Servers", out ServerTabViewModel? allServersTab))
        {
            throw new Exception("Could not add all servers tab");
        }

        if (!TryAddNewTab("HMW Servers", out ServerTabViewModel? hmwServersTab))
        {
            throw new Exception("Could not add HMW servers tab");
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

        ServerTabs.Remove(allServersTab);
        AllServersTab = allServersTab;
        HMWServersTab = hmwServersTab;
        FavouritesTab = favouritesTab;

        SelectedTab = HMWServersTab;

        H2MLauncherSettings oldSettings = _h2MLauncherOptions.CurrentValue;
        _h2MLauncherOptions.OnChange((newSettings, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!oldSettings.IW4MMasterServerUrl.Equals(newSettings.IW4MMasterServerUrl) ||
                    !oldSettings.HMWMasterServerUrl.Equals(newSettings.HMWMasterServerUrl))
                {
                    // refresh servers when master server url changes
                    RefreshServersCommand.Execute(null);
                }

                // reset filter to stored values
                AdvancedServerFilter.ResetViewModel(newSettings.ServerFilter);

                // reset shortcuts to stored values
                Shortcuts.ResetViewModel(newSettings.KeyBindings);

                OnPropertyChanged(nameof(IsMatchmakingEnabled));
                EnterMatchmakingCommand.NotifyCanExecuteChanged();

                oldSettings = newSettings;
            });
        });

        // initialize server filter view model with stored values
        AdvancedServerFilter.ResetViewModel(_h2MLauncherOptions.CurrentValue.ServerFilter);

        // initialize shortcut key bindings with stored values
        Shortcuts.ResetViewModel(_h2MLauncherOptions.CurrentValue.KeyBindings);

        _h2MCommunicationService.GameDetection.GameDetected += H2MCommunicationService_GameDetected;
        _h2MCommunicationService.GameDetection.GameExited += H2MCommunicationService_GameExited;
        _h2MCommunicationService.GameDetection.Error += GameDetection_Error;
        _h2MCommunicationService.GameCommunication.GameStateChanged += H2MCommunicationService_GameStateChanged;
        _h2MCommunicationService.GameCommunication.Stopped += H2MGameCommunication_Stopped;
        _mapsProvider.MapsChanged += MapsProvider_InstalledMapsChanged;

        _serverJoinService.ServerJoined += ServerJoinService_ServerJoined;
        _onlineService.StateChanged += OnlineService_StateChanged;

        socialOverviewViewModel.Friends.CreatePartyCommand.Execute(null);

        IsActive = true;
    }

    private void OnlineService_StateChanged(PlayerState oldState, PlayerState newState)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Automatically open the matchmaking dialog when the client state switches to matchmaking or queueing

            if (MatchmakingViewModel is not null)
            {
                // already open
                return;
            }

            if (oldState is PlayerState.Disconnected or PlayerState.Connected or PlayerState.Joined &&
                newState is PlayerState.Matchmaking or PlayerState.Queued)
            {
                MatchmakingViewModel = new(
                    _matchmakingService,
                    _queueingService,
                    _onlineService,
                    _serverDataService,
                    _serverJoinService)
                {
                    CloseOnLeave = newState is PlayerState.Queued
                };

                // Fire and forget dialog to not block the event raising thread
                _dialogService.ShowDialogAsync<QueueDialogView>(MatchmakingViewModel)
                   .ContinueWith(_ =>
                   {
                       MatchmakingViewModel = null;
                   }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        });
    }

    private void ServerJoinService_ServerJoined(IServerConnectionDetails server, JoinKind kind)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ServerViewModel? serverViewModel = server as ServerViewModel ?? FindServerViewModel(server);
            UpdateRecentJoinTime(serverViewModel, DateTime.Now);

            StatusText = $"Joined {server.Ip}:{server.Port}";
        });
    }

    private void MapsProvider_InstalledMapsChanged(IMapsProvider provider)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var serverViewModel in AllServersTab.Servers)
            {
                serverViewModel.HasMap = provider.InstalledMaps.Contains(serverViewModel.Map);
            }
        });
    }

    private void H2MGameCommunication_Stopped(Exception? exception)
    {
        GameState.State = null;

        if (exception is null)
        {
            return;
        }

        string dialogText;
        MessageBoxButton dialogButtons;
        if (_h2MCommunicationService.GameDetection.DetectedGame is not null)
        {
            dialogText = "It seems like the game communication has crashed. Click 'OK' to restart it.";
            dialogButtons = MessageBoxButton.OKCancel;
        }
        else
        {
            dialogText = "It seems like the game communication has crashed. It will be restarted when the game is detected.";
            dialogButtons = MessageBoxButton.OK;
        }

        if (_dialogService.OpenTextDialog("Error", dialogText, dialogButtons) == true)
        {
            _h2MCommunicationService.StartGameCommunication();
        }
    }

    private void H2MCommunicationService_GameStateChanged(GameState newState)
    {
        GameState.State = newState;
    }

    private void H2MCommunicationService_GameDetected(DetectedGame detectedGame)
    {
        GameState.DetectedGame = detectedGame;
    }

    private void H2MCommunicationService_GameExited()
    {
        GameState.DetectedGame = null;
    }

    private void GameDetection_Error(Exception? obj)
    {
        if (_dialogService.OpenTextDialog("Error",
            "It seems like the game detection has crashed. Would you like to restart it?",
            MessageBoxButton.YesNo) == true)
        {
            _h2MCommunicationService.GameDetection.StartGameDetection();
        }
    }

    private void ShowSettings()
    {
        SettingsViewModel settingsViewModel = new(_h2MLauncherOptions);

        // make sure all active hotkeys are disabled when settings are open
        foreach (var shortcut in Shortcuts.Shortcuts)
        {
            shortcut.IsHotkeyEnabled = false;
        }

        if (_dialogService.OpenDialog<SettingsDialogView>(settingsViewModel) == true)
        {
            // settings saved;
        }

        // re-enable hotkeys
        foreach (var shortcut in Shortcuts.Shortcuts)
        {
            shortcut.IsHotkeyEnabled = true;
        }
    }

    private void ShowServerFilter()
    {
        if (_dialogService.OpenDialog<FilterDialogView>(AdvancedServerFilter) == true)
        {
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
        return _h2MLauncherOptions.CurrentValue.FavouriteServers;
    }

    // Method to get user's recent servers from settings.
    public List<RecentServerInfo> GetRecentsFromSettings()
    {
        return _h2MLauncherOptions.CurrentValue.RecentServers;
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
        Process.Start(LauncherService.LauncherPath);
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

            if (!string.IsNullOrEmpty(_h2MLauncherOptions.CurrentValue.MWRLocation))
            {
                string? gameDirectory = Path.GetDirectoryName(_h2MLauncherOptions.CurrentValue.MWRLocation);

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

    private async Task GetServerInfo(
        IGameServerInfoService<IServerConnectionDetails> service,
        IList<ServerConnectionDetails> servers,
        CancellationToken cancellationToken)
    {
        IAsyncEnumerable<(IServerConnectionDetails, GameServerInfo?)> responses = await service.GetInfoAsync(
            servers,
            sendSynchronously: false,
            requestTimeoutInMs: 2000,
            cancellationToken: cancellationToken);

        // Start by sending info requests to the game servers
        // NOTE: we are using Task.Run to run this in a background thread,
        // because the non async timer blocks the UI
        await Task.Run(async () =>
        {
            try
            {
                await foreach ((IServerConnectionDetails server, GameServerInfo? info) in responses.ConfigureAwait(false).WithCancellation(cancellationToken))
                {
                    if (info is not null)
                    {
                        Application.Current.Dispatcher.Invoke(
                            () => OnGameServerInfoReceived(server, info),
                            DispatcherPriority.Render,
                            cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // canceled
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Could not fetch server info due to an unknown error.");
            }
        }, CancellationToken.None);
    }

    private async Task LoadServersAsync()
    {
        await _loadCancellation.CancelAsync();

        _loadCancellation = new();
        using CancellationTokenSource timeoutCancellation = new(5000);
        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _loadCancellation.Token, timeoutCancellation.Token);

        try
        {
            StatusText = "Refreshing servers...";

            AllServersTab.Servers.Clear();
            HMWServersTab.Servers.Clear();
            FavouritesTab.Servers.Clear();
            RecentsTab.Servers.Clear();

            // Get servers from the master(s)

            Task[] serverInfoTasks = await _masterServerService.FetchServersAsync(linkedCancellation.Token)
                .ToObservable()
                .Buffer(TimeSpan.FromSeconds(0.5))
                .Where(batch => batch.Count > 0)
                .Select(batch => GetServerInfo(_tcpGameServerCommunicationService, batch, linkedCancellation.Token))
                .ToArray();

            await Task.WhenAll(serverInfoTasks);

            StatusText = "Ready";
        }
        catch (OperationCanceledException ex)
        {
            // canceled
            Debug.WriteLine($"LoadServersAsync cancelled: {ex.Message}");
        }
        catch (Exception ex)
        {
            _errorHandlingService.HandleException(ex, "Could not refresh servers due to an unknown error.");
        }
    }

    private void OnGameServerInfoReceived(IServerConnectionDetails server, GameServerInfo serverInfo)
    {
        List<SimpleServerInfo> userFavorites = GetFavoritesFromSettings();
        List<RecentServerInfo> userRecents = GetRecentsFromSettings();

        bool isFavorite = userFavorites.Any(fav => fav.ServerIp == server.Ip && fav.ServerPort == server.Port);
        RecentServerInfo? recentInfo = userRecents.FirstOrDefault(recent => recent.ServerIp == server.Ip && recent.ServerPort == server.Port);

        ServerViewModel serverViewModel = new()
        {
            GameServerInfo = serverInfo,
            Ip = server.Ip,
            Port = server.Port,
            HostName = serverInfo.HostName,
            ClientNum = serverInfo.Clients - serverInfo.Bots,
            MaxClientNum = serverInfo.MaxClients,
            Game = serverInfo.GameName,
            GameType = serverInfo.GameType,
            GameTypeDisplayName = _resourceSettings.Value.GetGameTypeDisplayName(serverInfo.GameType),
            Map = serverInfo.MapName,
            MapDisplayName = _resourceSettings.Value.GetMapDisplayName(serverInfo.MapName),
            HasMap = _mapsProvider.InstalledMaps.Contains(serverInfo.MapName) || !_h2MLauncherOptions.Value.WatchGameDirectory,
            IsPrivate = serverInfo.IsPrivate,
            Ping = serverInfo.Ping,
            BotsNum = serverInfo.Bots,
            Protocol = serverInfo.Protocol,
            PrivilegedSlots = serverInfo.PrivilegedSlots,
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

        if (serverViewModel.Protocol == 3) // == HMW
        {
            HMWServersTab.Servers.Add(serverViewModel);
        }
    }

    private async Task JoinServer(ServerViewModel? serverViewModel)
    {
        if (serverViewModel is null)
            return;

        await _serverJoinService.JoinServer(serverViewModel, JoinKind.Normal);
    }

    private Task<JoinServerResult> ReconnectServer()
    {
        return _serverJoinService.JoinLastServer();
    }

    private Task<bool> DisconnectServer()
    {
        return _h2MCommunicationService.Disconnect();
    }

    private bool CheckGameRunning()
    {
        if (_h2MCommunicationService.GameDetection.DetectedGame is not null)
        {
            return true;
        }

        bool? dialogResult = _dialogService.OpenTextDialog(
            title: "Game not running",
            text: "Matchmaking is only available when the game is running. Do you want to launch the game?",
            acceptButtonText: "Launch Game",
            cancelButtonText: "Cancel");

        if (dialogResult == true)
        {
            _h2MCommunicationService.LaunchH2MMod();
        }

        return false;
    }

    private async Task EnterMatchmaking()
    {
#if DEBUG == false
        if (!CheckGameRunning())
        {
            return;
        }
#endif

        MatchmakingViewModel = new(
            _matchmakingService,
            _queueingService,
            _onlineService,
            _serverDataService,
            _serverJoinService);

        _dialogService.OpenDialog<QueueDialogView>(MatchmakingViewModel);

        MatchmakingViewModel = null;

        await Task.CompletedTask;
    }

    private ServerViewModel? FindServerViewModel(IServerConnectionDetails server)
    {
        return AllServersTab.Servers.FirstOrDefault(s =>
                (server.Ip == s.Ip || server.Ip == s.GameServerInfo?.Address?.Address?.GetRealAddress()?.ToString()) &&
                server.Port == s.Port);
    }

    public void Receive(SelectServerMessage message)
    {
        if (SelectedTab != HMWServersTab)
        {
            SelectedTab = HMWServersTab;
        }

        ServerViewModel? serverViewModel = FindServerViewModel(message.Value);
        if (serverViewModel is not null && SelectedTab.Servers.Contains(serverViewModel))
        {
            HMWServersTab.SelectedServer = serverViewModel;
        }
    }

    private void LaunchH2M()
    {
        _h2MCommunicationService.LaunchH2MMod();
    }

    public void Dispose()
    {
        _h2MCommunicationService.GameDetection.GameDetected -= H2MCommunicationService_GameDetected;
        _h2MCommunicationService.GameDetection.GameExited -= H2MCommunicationService_GameExited;
        _h2MCommunicationService.GameDetection.Error -= GameDetection_Error;
        _h2MCommunicationService.GameCommunication.GameStateChanged -= H2MCommunicationService_GameStateChanged;
        _h2MCommunicationService.GameCommunication.Stopped -= H2MGameCommunication_Stopped;
        _mapsProvider.MapsChanged -= MapsProvider_InstalledMapsChanged;
        _serverJoinService.ServerJoined -= ServerJoinService_ServerJoined;
    }
}
