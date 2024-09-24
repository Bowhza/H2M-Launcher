using System.Net;

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

using Flurl;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Game.Models;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Matchmaking;

public sealed class MatchmakingService : IAsyncDisposable
{
    private readonly HubConnection _connection;

    private readonly H2MCommunicationService _h2MCommunicationService;
    private readonly IGameCommunicationService _gameCommunicationService;
    private readonly IGameDetectionService _gameDetectionService;
    private readonly IPlayerNameProvider _playerNameProvider;
    private readonly CachedServerDataService _serverDataService;
    private readonly IGameServerInfoService<ServerConnectionDetails> _tcpGameServerInfoService;
    private readonly IGameServerInfoService<ServerConnectionDetails> _udpGameServerInfoService;
    private readonly IMasterServerService _hmwMasterServerService;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IMapsProvider _mapsProvider;
    private readonly IEndpointResolver _endpointResolver;

    private readonly IOptionsMonitor<H2MLauncherSettings> _options;
    private readonly ILogger<MatchmakingService> _logger;

    private JoinServerInfo? _queuedServer = null;
    private readonly Dictionary<ServerConnectionDetails, string> _privatePasswords = [];

    public JoinServerInfo? Server => _queuedServer;

    //private record QueuedServer(string Ip, int Port) : IFullServerConnectionDetails
    //{
    //    public string? Password { get; set; }
    //}

    private bool _hasSeenConnecting = false;

    public int SearchAttempts { get; private set; }
    public int QueuePosition { get; private set; }
    public int TotalPlayersInQueue { get; private set; }

    private PlayerState _state = PlayerState.Disconnected;
    public PlayerState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _logger.LogTrace("Set queueing state to {queueingState}", value);

            PlayerState oldState = _state;
            _state = value;

            OnQueueingStateChanged(oldState, value);
        }
    }

    private MatchmakingPreferences? _matchmakingPreferences = null;
    private MatchSearchCriteria? _currentMatchSearchCriteria = null;
    public MatchSearchCriteria? MatchSearchCriteria
    {
        get => _currentMatchSearchCriteria;
        set
        {
            if (EqualityComparer<MatchSearchCriteria>.Default.Equals(_currentMatchSearchCriteria, value))
            {
                return;
            }

            MatchSearchCriteria? oldValue = _currentMatchSearchCriteria;
            _currentMatchSearchCriteria = value;

            if (value is not null)
            {
                MatchSearchCriteriaChanged?.Invoke(value);
            }
        }
    }

    public Playlist? Playlist { get; private set; }

    public DateTimeOffset MatchSearchStarted { get; private set; }

    public bool IsConnected => _connection.State is HubConnectionState.Connected;
    public bool IsConnecting => _connection.State is HubConnectionState.Connecting;

    public event Action<PlayerState, PlayerState>? QueueingStateChanged;
    public event Action<int, int>? QueuePositionChanged;
    public event Action<IServerConnectionDetails>? Joining;
    public event Action<IServerConnectionDetails>? Joined;
    public event Action<IServerConnectionDetails>? JoinFailed;
    public event Action<(string hostname, SearchMatchResult match)>? MatchFound;
    public event Action<MatchmakingError>? MatchmakingError;
    public event Action<IEnumerable<SearchMatchResult>>? Matches;
    public event Action<MatchSearchCriteria>? MatchSearchCriteriaChanged;
    public event Action? ConnectionStateChanged;

    public MatchmakingService(
        ILogger<MatchmakingService> logger,
        H2MCommunicationService h2MCommunicationService,
        IGameCommunicationService gameCommunicationService,
        IOptions<MatchmakingSettings> matchmakingSettings,
        IOptionsMonitor<H2MLauncherSettings> options,
        IPlayerNameProvider playerNameProvider,
        IMapsProvider mapsProvider,
        CachedServerDataService serverDataService,
        [FromKeyedServices("TCP")] IGameServerInfoService<ServerConnectionDetails> tcpGameServerInfoService,
        [FromKeyedServices("UDP")] IGameServerInfoService<ServerConnectionDetails> udpGameServerInfoService,
        [FromKeyedServices("HMW")] IMasterServerService hmwMasterServerService,
        IErrorHandlingService errorHandlingService,
        IGameDetectionService gameDetectionService,
        IEndpointResolver endpointResolver)
    {
        _logger = logger;
        _options = options;
        _gameCommunicationService = gameCommunicationService;
        _playerNameProvider = playerNameProvider;
        _mapsProvider = mapsProvider;
        _endpointResolver = endpointResolver;

        object queryParams = new
        {
            uid = Guid.NewGuid().ToString(),
            playerName = _playerNameProvider.PlayerName
        };

        _connection = new HubConnectionBuilder()
            .WithUrl(matchmakingSettings.Value.QueueingHubUrl.SetQueryParams(queryParams))
            .Build();

        _connection.On<JoinServerInfo, bool>("NotifyJoin", OnNotifyJoin);
        _connection.On<int, int>("QueuePositionChanged", OnQueuePositionChanged);
        _connection.On<DequeueReason>("RemovedFromQueue", OnRemovedFromQueue);
        _connection.On<IEnumerable<SearchMatchResult>>("SearchMatchUpdate", OnSearchMatchUpdate);
        _connection.On<string, SearchMatchResult>("MatchFound", OnMatchFound);
        _connection.On<MatchmakingError>("RemovedFromMatchmaking", OnRemovedFromMatchmaking);

        _connection.Closed += Connection_Closed;

        _h2MCommunicationService = h2MCommunicationService;
        _serverDataService = serverDataService;
        _tcpGameServerInfoService = tcpGameServerInfoService;
        _udpGameServerInfoService = udpGameServerInfoService;
        _hmwMasterServerService = hmwMasterServerService;
        _gameDetectionService = gameDetectionService;
        _errorHandlingService = errorHandlingService;

        gameCommunicationService.GameStateChanged += GameCommunicationService_GameStateChanged;
        gameCommunicationService.Stopped += GameCommunicationService_Stopped;
    }

    private void OnQueueingStateChanged(PlayerState oldState, PlayerState newState)
    {
        if (newState is PlayerState.Disconnected or PlayerState.Joined or PlayerState.Connected)
        {
            _queuedServer = null;
            _privatePasswords.Clear();
        }

        QueueingStateChanged?.Invoke(oldState, newState);
    }


    #region RPC Handlers
    private async Task<bool> OnNotifyJoin(JoinServerInfo serverInfo)
    {
        _logger.LogInformation("Received 'NotifyJoin' with {ip} and {port}", serverInfo.Ip, serverInfo.Port);

        _queuedServer = serverInfo;

        Joining?.Invoke(_queuedServer);

        string? password = serverInfo.Password ?? _privatePasswords.GetValueOrDefault((serverInfo.Ip, serverInfo.Port));
        JoinServerResult result = await WeakReferenceMessenger.Default.Send<JoinRequestMessage>(new(serverInfo, password, JoinKind.FromQueue));
        if (result is JoinServerResult.Success)
        {
            State = PlayerState.Joining;
            return true;
        }

        _logger.LogDebug("Could not join server, setting queueing state back to 'Connected'");
        State = PlayerState.Connected;

        JoinFailed?.Invoke(_queuedServer);
        return false;
    }

    private void OnQueuePositionChanged(int position, int totalPlayersInQueue)
    {
        _logger.LogInformation("Received update queue position {queuePosition}/{queueLength}", position, totalPlayersInQueue);

        QueuePosition = position;
        TotalPlayersInQueue = totalPlayersInQueue;
        State = PlayerState.Queued;

        QueuePositionChanged?.Invoke(position, totalPlayersInQueue);
    }

    private void OnRemovedFromQueue(DequeueReason reason)
    {
        State = PlayerState.Connected;

        _logger.LogInformation("Removed from queue. Reason: {reason}", reason);

        if (reason is DequeueReason.Unknown)
        {
            _errorHandlingService.HandleError("Removed from queue due to unknown reason.");
        }
        else if (reason is DequeueReason.MaxJoinAttemptsReached)
        {
            _errorHandlingService.HandleError("Removed from queue: Max join attempts reached.");
        }
    }

    private bool AdjustSearchCriteria(IEnumerable<SearchMatchResult> searchMatchResults)
    {
        if (MatchSearchCriteria is null || _matchmakingPreferences is null)
        {
            return false;
        }

        if (++SearchAttempts > 7 && _matchmakingPreferences.TryFreshGamesFirst)
        {
            // remove max score limit after 7 attempts
            MatchSearchCriteria = MatchSearchCriteria with
            {
                MaxScore = _matchmakingPreferences.SearchCriteria.MaxScore,
                MaxPlayersOnServer = _matchmakingPreferences.SearchCriteria.MaxPlayersOnServer
            };
        }

        if (SearchAttempts > 2 || SearchAttempts > 1 && _matchmakingPreferences.TryFreshGamesFirst)
        {
            // remove min player limit after 2 attempts
            MatchSearchCriteria = MatchSearchCriteria with
            {
                MinPlayers = Math.Max(1, _matchmakingPreferences.SearchCriteria.MinPlayers)
            };
        }

        if (!searchMatchResults.Any())
        {
            // min players is too high, or no server is available                    
            return false;
        }

        if (MatchSearchCriteria.MaxScore > 0 &&
            searchMatchResults.All(r => r.ServerScore is null || r.ServerScore > MatchSearchCriteria.MaxScore))
        {
            // max server score is too low for any server
            return false;
            //// up max score
            //CurrentMatchSearchCriteria = CurrentMatchSearchCriteria with
            //{
            //    MaxScore = (searchMatchResults.Min(p => p.ServerScore ?? CurrentMatchSearchCriteria.MaxScore) + 500)
            //};
        }

        return true;
    }

    private async Task OnSearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults)
    {
        _logger.LogInformation("Received match search results: {n}", searchMatchResults.Count());

        Matches?.Invoke(searchMatchResults);

        if (MatchSearchCriteria is null || State is not PlayerState.Matchmaking)
        {
            return;
        }

        try
        {
            // adjust the search criteria based on the possible matches
            bool adjustPing = AdjustSearchCriteria(searchMatchResults);

            // ping all servers and send updated data
            List<ServerConnectionDetails> serverConnectionDetails = searchMatchResults
                .Select(matchResult => new ServerConnectionDetails(matchResult.ServerIp, matchResult.ServerPort))
                .ToList();

            List<ServerPing> serverPings = await PingServersAndFilter(serverConnectionDetails);

            _logger.LogDebug("Found {n}/{total} potential servers with ping <= {maxPing} ms",
                serverPings.Count(x => x.Ping <= MatchSearchCriteria.MaxPing), serverPings.Count, MatchSearchCriteria.MaxPing);

            if (adjustPing && MatchSearchCriteria.MaxPing > 0 &&
                serverPings.Count > 0 &&
                serverPings.All(p => p.Ping > MatchSearchCriteria.MaxPing))
            {
                // adjusting ping
                MatchSearchCriteria = MatchSearchCriteria with
                {
                    MaxPing = (int)(serverPings.Min(p => p.Ping) + 5)
                };
            }

            if (await _connection.InvokeAsync<bool>("UpdateSearchSession", MatchSearchCriteria, serverPings))
            {
                _logger.LogDebug("Updated search session: {@searchCriteria}", MatchSearchCriteria);
            }
            else
            {
                _logger.LogWarning("Could not update search session");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during matchmaking");
        }
    }

    private async Task<List<ServerPing>> PingServersAndFilter(IReadOnlyList<ServerConnectionDetails> servers)
    {
        _logger.LogDebug("Pinging {n} servers...", servers.Count);

        IReadOnlySet<ServerConnectionDetails> hmwServers = await _hmwMasterServerService.GetServersAsync(CancellationToken.None);
        var tcpResponses = await _tcpGameServerInfoService.GetInfoAsync(servers.Where(hmwServers.Contains), requestTimeoutInMs: 3000);
        var udpResponses = await _udpGameServerInfoService.GetInfoAsync(servers.Where(s => !hmwServers.Contains(s)), requestTimeoutInMs: 3000);

        return await tcpResponses
            .Concat(udpResponses)
            .Where(res => res.info is not null && _mapsProvider.InstalledMaps.Contains(res.info.MapName)) // filter out servers with missing maps
            .Select(res => new ServerPing(res.server.Ip, res.server.Port, (uint)res.info!.Ping))
            .ToListAsync();
    }

    private void OnMatchFound(string hostName, SearchMatchResult matchResult)
    {
        _logger.LogInformation("Received match found result: {matchResult}", matchResult);

        MatchFound?.Invoke((hostName, matchResult));
    }

    private void OnRemovedFromMatchmaking(MatchmakingError reason)
    {
        State = PlayerState.Connected;
        _logger.LogInformation("Removed from matchmaking. Reason: {reason}", reason);
        MatchmakingError?.Invoke(reason);
    }

    #endregion

    private async void GameCommunicationService_GameStateChanged(GameState gameState)
    {
        if (State is not (PlayerState.Joining or PlayerState.Queued))
        {
            // ignore events when not in queue
            return;
        }

        if (gameState.IsConnected && gameState.Endpoint is not null)
        {
            // we are connected to something, check whether it has to do with us
            JoinServerInfo? queuedServer = GetQueuedServer(gameState);
            if (queuedServer is null)
            {
                // nope
                return;
            }

            _logger.LogDebug("Game connected to {ip}, sending join ack...", queuedServer.Ip);

            // send join confirmation to server
            await AcknowledgeJoin(true);

            // we are now joined :)
            State = PlayerState.Joined;
            Joined?.Invoke(queuedServer);
        }
        else if (gameState.IsInMainMenu && _queuedServer is not null && _hasSeenConnecting)
        {
            // something went wrong with joining, we have been connection and now are in the main menu again :(

            if (State is PlayerState.Joining)
            {
                // we were joining, so that's probably the queued server.
                // tell server we could not join, maybe the server got full in the meantime
                // (only if we were actually joining from the queue, not force joining.
                // Otherwise, we want to stay in the queue because the failed join attempt doesn't count)
                _logger.LogInformation("Could not join server, sending failed join ack...");
                await AcknowledgeJoin(false);

                State = PlayerState.Queued;
            }

            // notify others that a join failed
            JoinFailed?.Invoke(_queuedServer);
        }
        else if (gameState.IsConnecting && !gameState.IsPrivateMatch)
        {
            // connecting to something public
            JoinServerInfo? queuedServer = GetQueuedServer(gameState);
            if (queuedServer is not null)
            {
                // set this flag so we dont recognize a previous server connected to as the current server
                _hasSeenConnecting = true;
            }
        }
    }

    private async void GameCommunicationService_Stopped(Exception? obj)
    {
        if (State is PlayerState.Joining or PlayerState.Queued or PlayerState.Matchmaking)
        {
            await LeaveQueueAsync();
        }
    }

    private JoinServerInfo? GetQueuedServer(GameState gameState)
    {
        if (gameState.Endpoint is null)
        {
            // game state has no connected endpoint
            return null;
        }

        // (unfortunately we cannot use port because the game gives us the local UDP port)
        string gameStateIp = gameState.Endpoint.Address.GetRealAddress().ToString();
        if (_queuedServer?.Ip == gameStateIp)
        {
            // ip matches the queued server
            return _queuedServer;
        }

        return null;
    }

    private Task Connection_Closed(Exception? arg)
    {
        State = PlayerState.Disconnected;
        ConnectionStateChanged?.Invoke();

        return Task.CompletedTask;
    }

    public async Task StartConnection(CancellationToken cancellationToken = default)
    {
        Task startConnectionTask = _connection.StartAsync(cancellationToken);
        try
        {
            ConnectionStateChanged?.Invoke();
            await startConnectionTask;
            State = PlayerState.Connected;
        }
        finally
        {
            ConnectionStateChanged?.Invoke();
        }
    }

    public async Task AcknowledgeJoin(bool joinSuccessful)
    {
        try
        {
            await _connection.SendAsync("JoinAck", joinSuccessful).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending join ack");
        }
    }

    public async Task<bool> JoinQueueAsync(IServerInfo server, string? privatePassword)
    {
        try
        {
            if (!_options.CurrentValue.ServerQueueing ||
                !_options.CurrentValue.GameMemoryCommunication ||
                !_gameDetectionService.IsGameDetectionRunning)
            {
                return false;
            }

            _logger.LogDebug("Joining server queue...");
            if (_connection.State is HubConnectionState.Disconnected)
            {
                await StartConnection();
            }

            serverEndpoint ??= await _endpointResolver.GetEndpointAsync(server, CancellationToken.None);
            if (serverEndpoint is null)
            {
                _logger.LogDebug("Could not resolve endpoint for {@server}", server);
                return false;
            }

            bool joinedSuccesfully = await _connection.InvokeAsync<bool>("JoinQueue", server.Ip, server.Port, server.InstanceId);
            if (!joinedSuccesfully)
            {
                _logger.LogDebug("Could not join queue for {serverIp}:{serverPort}",
                    server.Ip, server.Port);
                return false;
            }

            _logger.LogInformation("Joined server queue for {serverIp}:{serverPort}",
                server.Ip, server.Port);

            if (privatePassword is not null)
            {
                _privatePasswords[(server.Ip, server.Port)] = privatePassword;
            };

            _queuedServer = new JoinServerInfo(serverEndpoint.Address.GetRealAddress().ToString(), serverEndpoint.Port, server.ServerName);
            State = PlayerState.Queued;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while joining server queue");
            return false;
        }
    }

    public async Task LeaveQueueAsync()
    {
        try
        {
            if (_connection.State is HubConnectionState.Connected)
            {
                await _connection.SendAsync("LeaveQueue");
                State = PlayerState.Connected;
                Playlist = null;
                MatchSearchCriteria = null;
                SearchAttempts = 0;
                _logger.LogInformation("Server queue left.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while leaving server queue");
        }
    }

    public async Task<bool> EnterMatchmakingAsync(MatchmakingPreferences? searchPreferences = null)
    {
        try
        {
            Playlist? playlist = await _serverDataService.GetDefaultPlaylist(CancellationToken.None);
            if (playlist is null)
            {
                return false;
            }

            return await EnterMatchmakingAsync(playlist, searchPreferences).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting default playlist");
            return false;
        }
    }

    private static int GetMinWhenDefault(int valueMaybeDefault, int value2, int defaultValue = -1)
    {
        return valueMaybeDefault == defaultValue ? value2 : Math.Min(valueMaybeDefault, value2);
    }

    public async Task<bool> EnterMatchmakingAsync(Playlist playlist, MatchmakingPreferences? searchPreferences = null)
    {
        try
        {
            if (!_options.CurrentValue.ServerQueueing ||
                !_options.CurrentValue.GameMemoryCommunication ||
                !_gameDetectionService.IsGameDetectionRunning ||
                !_gameCommunicationService.IsGameCommunicationRunning)
            {
                return false;
            }

            if (State is PlayerState.Queued or PlayerState.Joining or PlayerState.Matchmaking)
            {
                return false;
            }

            if (playlist.Servers is null || playlist.Servers.Count == 0)
            {
                return false;
            }

            _logger.LogDebug("Entering matchmaking...");

            if (_connection.State is HubConnectionState.Disconnected)
            {
                await StartConnection();
            }

            _matchmakingPreferences = searchPreferences ??= new MatchmakingPreferences()
            {
                SearchCriteria = new MatchSearchCriteria()
                {
                    MaxPing = 300,
                    MinPlayers = 6,
                }
            };

            MatchmakingPreferences pref = _matchmakingPreferences;
            MatchSearchCriteria sc = _matchmakingPreferences.SearchCriteria;
            MatchSearchCriteria initialSearchCriteria = new()
            {
                MinPlayers = Math.Max(sc.MinPlayers, 6),
                MaxPing = GetMinWhenDefault(sc.MaxPing, 28),
                MaxScore = pref.TryFreshGamesFirst ? GetMinWhenDefault(sc.MaxScore, 2000) : sc.MaxScore,
                MaxPlayersOnServer = pref.TryFreshGamesFirst ? 0 : sc.MaxPlayersOnServer
            };

            SearchAttempts = 0;
            Playlist = playlist;
            MatchSearchCriteria = initialSearchCriteria;

            bool success = await _connection.InvokeAsync<bool>("SearchMatch", initialSearchCriteria, playlist.Servers);
            if (!success)
            {
                _logger.LogDebug("Could not enter matchmaking for playlist '{playlist}'", playlist.Id);
                return false;
            }

            State = PlayerState.Matchmaking;
            _logger.LogInformation("Entered matchmaking queue for playlist '{playlist}'", playlist.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while entering matchmaking");
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        _gameCommunicationService.GameStateChanged -= GameCommunicationService_GameStateChanged;
        _connection.Closed -= Connection_Closed;
        return _connection.DisposeAsync();
    }
}
