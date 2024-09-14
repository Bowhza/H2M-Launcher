using System.Net;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Services
{
    public enum MatchmakingError
    {
        QueueingFailed
    }

    public enum DequeueReason
    {
        Unknown,
        UserLeave,
        MaxJoinAttemptsReached,
        JoinTimeout,
        Disconnect,
        Joined,
        JoinFailed
    }

    public enum PlayerState
    {
        Disconnected,
        Connected,
        Matchmaking,
        Queued,
        Joining,
        Joined
    }


    public sealed class MatchmakingService : IAsyncDisposable
    {
        private readonly HubConnection _connection;

        private readonly H2MCommunicationService _h2MCommunicationService;
        private readonly IGameCommunicationService _gameCommunicationService;
        private readonly IPlayerNameProvider _playerNameProvider;
        private readonly CachedServerDataService _serverDataService;
        private readonly GameServerCommunicationService<ServerConnectionDetails> _gameServerCommunicationService;

        private readonly IOptionsMonitor<H2MLauncherSettings> _options;
        private readonly ILogger<MatchmakingService> _logger;

        private record QueuedServer(string Ip, int Port)
        {
            public bool HasSeenConnecting { get; set; }

            public string? PrivatePassword { get; set; }
        }

        private QueuedServer? _queuedServer = null;
        private readonly Dictionary<ServerConnectionDetails, string> _privatePasswords = [];

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

        public event Action<PlayerState>? QueueingStateChanged;
        public event Action<int, int>? QueuePositionChanged;
        public event Action<(string ip, int port)>? Joining;
        public event Action<(string ip, int port)>? Joined;
        public event Action<(string ip, int port)>? JoinFailed;
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
            CachedServerDataService serverDataService,
            GameServerCommunicationService<ServerConnectionDetails> gameServerCommunicationService)
        {
            _logger = logger;
            _options = options;
            _gameCommunicationService = gameCommunicationService;
            _playerNameProvider = playerNameProvider;

            _connection = new HubConnectionBuilder()
                .WithUrl(matchmakingSettings.Value.QueueingHubUrl)
                .Build();

            _connection.On<string, int, bool>("NotifyJoin", OnNotifyJoin);
            _connection.On<int, int>("QueuePositionChanged", OnQueuePositionChanged);
            _connection.On<DequeueReason>("RemovedFromQueue", OnRemovedFromQueue);
            _connection.On<IEnumerable<SearchMatchResult>>("SearchMatchUpdate", OnSearchMatchUpdate);
            _connection.On<string, SearchMatchResult>("MatchFound", OnMatchFound);
            _connection.On<MatchmakingError>("RemovedFromMatchmaking", OnRemovedFromMatchmaking);

            _connection.Closed += Connection_Closed;

            _h2MCommunicationService = h2MCommunicationService;
            _gameCommunicationService.GameStateChanged += GameCommunicationService_GameStateChanged;
            _serverDataService = serverDataService;
            _gameServerCommunicationService = gameServerCommunicationService;
        }

        private void OnQueueingStateChanged(PlayerState oldState, PlayerState newState)
        {
            if (newState is PlayerState.Disconnected or PlayerState.Joined or PlayerState.Connected)
            {
                _queuedServer = null;
                _privatePasswords.Clear();
            }

            QueueingStateChanged?.Invoke(newState);
        }


        #region RPC Handlers
        private bool OnNotifyJoin(string ip, int port)
        {
            _logger.LogInformation("Received 'NotifyJoin' with {ip} and {port}", ip, port);

            _queuedServer = new QueuedServer(ip, port);

            Joining?.Invoke((ip, port));

            if (_h2MCommunicationService.JoinServer(ip, port.ToString(), _privatePasswords.GetValueOrDefault(new(ip, port))))
            {
                State = PlayerState.Joining;
                return true;
            }

            _logger.LogDebug("Could not join server, setting queueing state back to 'Connected'");
            State = PlayerState.Connected;

            JoinFailed?.Invoke((ip, port));
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
        }

        private bool AdjustSearchCriteria(IEnumerable<SearchMatchResult> searchMatchResults)
        {
            if (MatchSearchCriteria is null || _matchmakingPreferences is null)
            {
                return false;
            }

            if (++SearchAttempts > 7 && _matchmakingPreferences.TryFreshGamesFirst)
            {
                // remove max score limit after 4 attempts
                MatchSearchCriteria = MatchSearchCriteria with
                {
                    MaxScore = _matchmakingPreferences.SearchCriteria.MaxScore,
                    MaxPlayersOnServer = _matchmakingPreferences.SearchCriteria.MaxPlayersOnServer
                };
            }

            if (SearchAttempts > 2 || (SearchAttempts > 1 && _matchmakingPreferences.TryFreshGamesFirst))
            {
                // remove min player limit after 4 attempts
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

                List<ServerPing> serverPings = await PingServers(serverConnectionDetails);

                _logger.LogDebug("Found {n}/{total} potential servers with ping <= {maxPing} ms",
                    serverPings.Count(x => x.Ping <= MatchSearchCriteria.MaxPing), serverPings.Count, MatchSearchCriteria.MaxPing);

                if (adjustPing && MatchSearchCriteria.MaxPing > 0 && serverPings.All(p => p.Ping > MatchSearchCriteria.MaxPing))
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

        private async Task<List<ServerPing>> PingServers(IReadOnlyList<ServerConnectionDetails> servers)
        {
            _logger.LogTrace("Pinging {n} servers...", servers.Count);

            var responses = await _gameServerCommunicationService.GetInfoAsync(servers, requestTimeoutInMs: 3000);

            return await responses
                .Where(res => res.info is not null)
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
                QueuedServer? queuedServer = GetQueuedServer(gameState);
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
                Joined?.Invoke((queuedServer.Ip, queuedServer.Port));
            }
            else if (gameState.IsInMainMenu && _queuedServer?.HasSeenConnecting == true)
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
                JoinFailed?.Invoke((_queuedServer.Ip, _queuedServer.Port));
            }
            else if (gameState.IsConnecting && !gameState.IsPrivateMatch)
            {
                // connecting to something public
                QueuedServer? queuedServer = GetQueuedServer(gameState);
                if (queuedServer is not null)
                {
                    // set this flag so we dont recognize a previous server connected to as the current server
                    queuedServer.HasSeenConnecting = true;
                }
            }
        }

        private QueuedServer? GetQueuedServer(GameState gameState)
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

        public async Task<bool> JoinQueueAsync(IW4MServer server, IPEndPoint serverEndpoint, string? privatePassword)
        {
            try
            {
                if (!_options.CurrentValue.ServerQueueing || !_options.CurrentValue.GameMemoryCommunication)
                {
                    return false;
                }

                _logger.LogDebug("Joining server queue...");
                if (_connection.State is HubConnectionState.Disconnected)
                {
                    await StartConnection();
                }

                string playerName = _playerNameProvider.PlayerName;

                bool joinedSuccesfully = await _connection.InvokeAsync<bool>("JoinQueue", server.Ip, server.Port, server.Instance.Id, playerName);
                if (!joinedSuccesfully)
                {
                    _logger.LogDebug("Could not join queue as '{playerName}' for {serverIp}:{serverPort}",
                        playerName, server.Ip, server.Port);
                    return false;
                }

                _logger.LogInformation("Joined server queue as '{playerName}' for {serverIp}:{serverPort}",
                    playerName, server.Ip, server.Port);

                if (privatePassword is not null)
                {
                    _privatePasswords[new ServerConnectionDetails(server.Ip, server.Port)] = privatePassword;
                };

                _queuedServer = new QueuedServer(serverEndpoint.Address.GetRealAddress().ToString(), serverEndpoint.Port);
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
                        MinPlayers = 8,
                    }
                };

                MatchmakingPreferences pref = _matchmakingPreferences;
                MatchSearchCriteria sc = _matchmakingPreferences.SearchCriteria;
                MatchSearchCriteria initialSearchCriteria = new()
                {
                    MinPlayers = Math.Max(sc.MinPlayers, 8),
                    MaxPing = GetMinWhenDefault(sc.MaxPing, 28),
                    MaxScore = pref.TryFreshGamesFirst ? GetMinWhenDefault(sc.MaxScore, 2000) : sc.MaxScore,
                    MaxPlayersOnServer = pref.TryFreshGamesFirst ? 0 : sc.MaxPlayersOnServer
                };

                SearchAttempts = 0;
                Playlist = playlist;
                MatchSearchCriteria = initialSearchCriteria;
                string playerName = _playerNameProvider.PlayerName;

                bool success = await _connection.InvokeAsync<bool>("SearchMatch", playerName, initialSearchCriteria, playlist.Servers);
                if (!success)
                {
                    _logger.LogDebug("Could not enter matchmaking for playlist '{playlist}' as '{playerName}'", playlist.Id, playerName);
                    return false;
                }

                State = PlayerState.Matchmaking;
                _logger.LogInformation("Entered matchmaking queue for playlist '{playlist}' as '{playerName}' for ", playlist.Id, playerName);

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
}
