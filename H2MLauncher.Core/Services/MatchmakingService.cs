using System.Linq;
using System.Net;
using System.Reactive;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Services
{
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
        private readonly GameServerCommunicationService<IServerConnectionDetails> _gameServerCommunicationService;

        private readonly IOptionsMonitor<H2MLauncherSettings> _options;
        private readonly ILogger<MatchmakingService> _logger;

        private record QueuedServer(string Ip, int Port)
        {
            public bool HasSeenConnecting { get; set; }

            public string? PrivatePassword { get; set; }
        }

        private QueuedServer? _queuedServer = null;
        private readonly Dictionary<ServerConnectionDetails, string> _privatePasswords = [];

        public int QueuePosition { get; private set; }
        public int TotalPlayersInQueue { get; private set; }

        private PlayerState _queueingState = PlayerState.Disconnected;
        public PlayerState QueueingState
        {
            get => _queueingState;
            set
            {
                if (_queueingState == value)
                {
                    return;
                }

                _logger.LogTrace("Set queueing state to {queueingState}", value);

                PlayerState oldState = _queueingState;
                _queueingState = value;

                OnQueueingStateChanged(oldState, value);
            }
        }

        public bool IsConnected => _connection.State is HubConnectionState.Connected;

        public event Action<PlayerState>? QueueingStateChanged;
        public event Action<int, int>? QueuePositionChanged;
        public event Action<(string ip, int port)>? Joining;
        public event Action<(string ip, int port)>? Joined;
        public event Action<(string ip, int port)>? JoinFailed;

        public MatchmakingService(
            ILogger<MatchmakingService> logger,
            H2MCommunicationService h2MCommunicationService,
            IGameCommunicationService gameCommunicationService,
            IOptions<MatchmakingSettings> matchmakingSettings,
            IOptionsMonitor<H2MLauncherSettings> options,
            IPlayerNameProvider playerNameProvider,
            CachedServerDataService serverDataService,
            GameServerCommunicationService<IServerConnectionDetails> gameServerCommunicationService)
        {
            _logger = logger;
            _options = options;
            _gameCommunicationService = gameCommunicationService;
            _playerNameProvider = playerNameProvider;

            _connection = new HubConnectionBuilder()
                .WithUrl(matchmakingSettings.Value.QueueingHubUrl)
                //.WithAutomaticReconnect()
                .Build();

            _connection.On<string, int, bool>("NotifyJoin", OnNotifyJoin);
            _connection.On<int, int>("QueuePositionChanged", OnQueuePositionChanged);
            _connection.On<DequeueReason>("RemovedFromQueue", OnRemovedFromQueue);
            _connection.On<IEnumerable<SearchMatchResult>>("SearchMatchUpdate", OnSearchMatchUpdate);

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
                QueueingState = PlayerState.Joining;
                return true;
            }

            _logger.LogDebug("Could not join server, setting queueing state back to 'Connected'");
            QueueingState = PlayerState.Connected;
            return false;
        }

        private void OnQueuePositionChanged(int position, int totalPlayersInQueue)
        {
            _logger.LogInformation("Received update queue position {queuePosition}/{queueLength}", position, totalPlayersInQueue);

            QueuePosition = position;
            TotalPlayersInQueue = totalPlayersInQueue;

            QueuePositionChanged?.Invoke(position, totalPlayersInQueue);
        }

        private void OnRemovedFromQueue(DequeueReason reason)
        {
            QueueingState = PlayerState.Connected;

            _logger.LogInformation("Removed from queue. Reason: {reason}", reason);
        }

        private void OnSearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults)
        {
            _logger.LogInformation("Received match search results: {n}", searchMatchResults.Count());
        }

        #endregion

        private async void GameCommunicationService_GameStateChanged(GameState gameState)
        {
            if (QueueingState is not (PlayerState.Joining or PlayerState.Queued))
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
                QueueingState = PlayerState.Joined;
                Joined?.Invoke((queuedServer.Ip, queuedServer.Port));
            }
            else if (gameState.IsInMainMenu && _queuedServer?.HasSeenConnecting == true)
            {
                // something went wrong with joining, we have been connection and now are in the main menu again :(

                if (QueueingState is PlayerState.Joining)
                {
                    // we were joining, so that's probably the queued server.
                    // tell server we could not join, maybe the server got full in the meantime
                    // (only if we were actually joining from the queue, not force joining.
                    // Otherwise, we want to stay in the queue because the failed join attempt doesn't count)
                    _logger.LogInformation("Could not join server, sending failed join ack...");
                    await AcknowledgeJoin(false);

                    QueueingState = PlayerState.Queued;
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
            QueueingState = PlayerState.Disconnected;

            return Task.CompletedTask;
        }

        public async Task StartConnection()
        {
            await _connection.StartAsync();
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
                QueueingState = PlayerState.Queued;

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
                    QueueingState = PlayerState.Connected;
                    _logger.LogInformation("Server queue left.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while leaving server queue");
            }
        }

        public async Task<bool> EnterMatchmakingAsync()
        {
            try
            {
                Playlist? playlist = await _serverDataService.GetDefaultPlaylist(CancellationToken.None);
                if (playlist is null)
                {
                    return false;
                }

                return await EnterMatchmakingAsync(playlist, minPlayers: 8).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting default playlist");
                return false;
            }
        }

        public async Task<bool> EnterMatchmakingAsync(Playlist playlist, int minPlayers = 8, int maxPing = 80)
        {
            try
            {
                if (playlist.Servers is null || playlist.Servers.Count == 0)
                {
                    return false;
                }

                _logger.LogDebug("Entering matchmaking...");

                if (_connection.State is HubConnectionState.Disconnected)
                {
                    await StartConnection();
                }

                string playerName = _playerNameProvider.PlayerName;
                MatchSearchCriteria searchPreferences = new()
                {
                    MinPlayers = 8,
                    MaxPing = 80,
                };

                bool success = await _connection.InvokeAsync<bool>("SearchMatch", playerName, searchPreferences, playlist.Servers);
                if (!success)
                {
                    _logger.LogDebug("Could not enter matchmaking for playlist '{playlist}' as '{playerName}'", playlist.Id, playerName);
                    return false;
                }

                QueueingState = PlayerState.Matchmaking;
                _logger.LogInformation("Entered matchmaking queue for playlist '{playlist}' as '{playerName}' for ", playlist.Id, playerName);

                _ = Task.Run(async () =>
                {
                    List<IServerConnectionDetails> serverConnectionDetails = playlist.Servers.Select(address =>
                    {
                        string[] splitted = address.Split(':');
                        if (splitted.Length != 2)
                        {
                            return default;
                        }

                        string ip = splitted[0];
                        if (!int.TryParse(splitted[1], out int port))
                        {
                            return default;
                        }

                        return new ServerConnectionDetails(ip, port);
                    }).Where(s => s.Ip is not null)
                      .Cast<IServerConnectionDetails>().ToList();

                    while (QueueingState is PlayerState.Matchmaking)
                    {
                        try
                        {
                            _logger.LogDebug("Searching for servers with ping <= {maxPing}", maxPing);

                            var responses = await _gameServerCommunicationService.GetInfoAsync(serverConnectionDetails, requestTimeoutInMs: 3000);

                            List<ServerPing> serverPings = await responses
                                .Where(res => res.info is not null)
                                .Select(res => new ServerPing(res.server.Ip, res.server.Port, (uint)res.info!.Ping))
                                .ToListAsync();

                            _logger.LogDebug("Found {n}/{total} potential servers with ping <= {maxPing} ms", 
                                serverPings.Count(x => x.Ping <= maxPing), serverPings.Count, maxPing);

                            if (!await _connection.InvokeAsync<bool>("UpdateSearchSession", searchPreferences, serverPings))
                            {
                                _logger.LogWarning("Could not update search session");
                            }

                            await Task.Delay(2000);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during matchmaking");
                        }
                    }
                });

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
