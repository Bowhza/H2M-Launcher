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
        Queued,
        Joining,
        Joined
    }

    public sealed class MatchmakingService : IAsyncDisposable
    {
        private readonly H2MCommunicationService _h2MCommunicationService;

        private readonly HubConnection _connection;
        private readonly ILogger<MatchmakingService> _logger;
        private readonly IGameCommunicationService _gameCommunicationService;

        private string _joiningServerIp = "";
        private bool _hasSeenConnecting = false;
        private readonly Dictionary<(string ip, int port), string> _privatePasswords = [];

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
        public event Action<(string ip, int port)>? Joining;
        public event Action<int, int>? QueuePositionChanged;

        public MatchmakingService(ILogger<MatchmakingService> logger, H2MCommunicationService h2MCommunicationService,
            IGameCommunicationService gameCommunicationService, IOptions<MatchmakingSettings> matchmakingSettings)
        {
            _logger = logger;
            _gameCommunicationService = gameCommunicationService;

            _connection = new HubConnectionBuilder()
                .WithUrl(matchmakingSettings.Value.QueueingHubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, int, bool>("NotifyJoin", OnNotifyJoin);
            _connection.On<int, int>("QueuePositionChanged", OnQueuePositionChanged);
            _connection.On<DequeueReason>("RemovedFromQueue", OnRemovedFromQueue);

            _connection.Closed += Connection_Closed;

            _h2MCommunicationService = h2MCommunicationService;
            _gameCommunicationService.GameStateChanged += GameCommunicationService_GameStateChanged;
        }

        private void OnQueueingStateChanged(PlayerState oldState, PlayerState newState)
        {
            if (newState is PlayerState.Disconnected or PlayerState.Joined or PlayerState.Connected or PlayerState.Queued)
            {
                _joiningServerIp = "";
                _hasSeenConnecting = false;
                _privatePasswords.Clear();
            }

            QueueingStateChanged?.Invoke(newState);
        }


        #region RPC Handlers
        private bool OnNotifyJoin(string ip, int port)
        {
            _logger.LogInformation("Received 'NotifyJoin' with {ip} and {port}", ip, port);

            _joiningServerIp = ip;
            _hasSeenConnecting = false;

            Joining?.Invoke((ip, port));

            if (_h2MCommunicationService.JoinServer(ip, port.ToString(), _privatePasswords.GetValueOrDefault((ip, port))))
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

        #endregion

        private async void GameCommunicationService_GameStateChanged(GameState gameState)
        {
            if (QueueingState is not (PlayerState.Joining or PlayerState.Queued))
            {
                // ignore events when not joining
                return;
            }

            if (gameState.IsConnected &&
                gameState.Endpoint is not null &&
                gameState.Endpoint.Address.MapToIPv4().ToString().Equals(_joiningServerIp))
            {
                // we are connected to something

                _logger.LogDebug("Game connected to {ip}, sending join ack...", gameState.Endpoint.Address.ToString());

                // send join confirmation to server
                await AcknowledgeJoin(true);

                // we are now joined :)
                QueueingState = PlayerState.Joined;
            }

            if (gameState.IsInMainMenu && _hasSeenConnecting)
            {
                // something went wrong with joining, we have been connection and now are in the main menu again :(

                _logger.LogInformation("Could not join server, sending failed join ack...");

                // tell server we could not join, maybe the server got full in the meantime
                await AcknowledgeJoin(false);

                QueueingState = PlayerState.Queued;
            }

            if (gameState.IsConnecting && !gameState.IsPrivateMatch)
            {
                // set this flag so we dont recognize a previous server connected to as the current server
                // TODO: maybe compare IPs? could not match somehow and the unnecessarily block the queue
                _hasSeenConnecting = true;
            }
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

        public async Task<bool> JoinQueueAsync(IW4MServer server, string playerName, string? privatePassword)
        {
            try
            {
                _logger.LogDebug("Joining server queue...");
                if (_connection.State is not HubConnectionState.Connected)
                {
                    await StartConnection();
                }

                if (privatePassword is not null)
                {
                    _privatePasswords.Add((server.Ip, server.Port), privatePassword);
                };

                bool joinedSuccesfully = await _connection.InvokeAsync<bool>("JoinQueue", server.Ip, server.Port, server.Instance.Id, playerName);
                if (joinedSuccesfully)
                {
                    _logger.LogInformation("Joined server queue as '{playerName}' for {serverIp}:{serverPort}",
                        playerName, server.Ip, server.Port);

                    QueueingState = PlayerState.Queued;
                }

                return joinedSuccesfully;
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

        public ValueTask DisposeAsync()
        {
            _gameCommunicationService.GameStateChanged -= GameCommunicationService_GameStateChanged;
            _connection.Closed -= Connection_Closed;
            return _connection.DisposeAsync();
        }
    }
}
