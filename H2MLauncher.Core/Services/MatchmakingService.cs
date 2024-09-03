using System.Reactive.Linq;

using H2MLauncher.Core.Models;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

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

    public class MatchmakingService
    {
        private readonly H2MCommunicationService _h2MCommunicationService;

        private readonly HubConnection _connection;
        private readonly ILogger<MatchmakingService> _logger;
        private readonly IGameCommunicationService _gameCommunicationService;

        private string _joiningServerIp = "";
        private bool _hasSeenConnecting = false;

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

                _queueingState = value;
                QueueingStateChanged?.Invoke(value);
            }
        }

        public bool IsConnected => _connection.State is HubConnectionState.Connected;

        public event Action<PlayerState>? QueueingStateChanged;
        public event Action<(string ip, int port)>? Joining;
        public event Action<int, int>? QueuePositionChanged;

        public MatchmakingService(ILogger<MatchmakingService> logger, H2MCommunicationService h2MCommunicationService,
            IGameCommunicationService gameCommunicationService)
        {
            _logger = logger;
            _gameCommunicationService = gameCommunicationService;

            _connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7208/Queue")
                .Build();

            _connection.On("NotifyJoin", (string ip, int port) =>
            {
                logger.LogInformation("Received 'NotifyJoin' with {ip} and {port}", ip, port);

                _joiningServerIp = ip;
                Joining?.Invoke((ip, port));

                if (h2MCommunicationService.JoinServer(ip, port.ToString()))
                {
                    QueueingState = PlayerState.Joining;
                    return true;
                }

                QueueingState = PlayerState.Connected;

                return false;
            });

            _connection.On("QueuePositionChanged", (int position, int totalPlayersInQueue) =>
            {
                logger.LogInformation("Received update queue position {queuePosition}/{queueLength}", position, totalPlayersInQueue);
                QueuePosition = position;
                TotalPlayersInQueue = totalPlayersInQueue;
                QueuePositionChanged?.Invoke(position, totalPlayersInQueue);
            });

            _connection.On("RemoveFromQueue", (DequeueReason reason) =>
            {
                QueueingState = PlayerState.Connected;
                logger.LogInformation("Removed from queue. Reason: {reason}", reason);
            });

            _connection.Closed += Connection_Closed;

            _h2MCommunicationService = h2MCommunicationService;
            gameCommunicationService.GameStateChanged += GameCommunicationService_GameStateChanged;
        }

        private async void GameCommunicationService_GameStateChanged(GameState gameState)
        {
            if (QueueingState is not PlayerState.Joining)
            {
                // ignore events when not joining
                return;
            }

            if (gameState.IsConnected && gameState.Endpoint is not null)
            {
                // we are connected to something
                try
                {
                    _logger.LogDebug("Game connected to {ip}, sending join ack...", gameState.Endpoint.Address.ToString());

                    // send join confirmation to server
                    await _connection.SendAsync("JoinAck", true);
                    QueueingState = PlayerState.Joined;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while sending join ack");
                }
            }

            if (gameState.IsInMainMenu && _hasSeenConnecting)
            {
                // something went wrong with joining, we have been connection and now are in the main menu again :(

                _logger.LogInformation("Could not join server, sending failed join ack...");
                _hasSeenConnecting = false;

                // tell server we could not join, maybe the server got full in the meantime
                await _connection.SendAsync("JoinAck", false);
            }

            if (gameState.IsConnecting && !gameState.IsPrivateMatch)
            {
                // set this flag so we dont recognize a previous server connected to as the current server
                // TODO: maybe compare IPs? could not match somehow and the unnecessarily block the queue
                _hasSeenConnecting = true;
            }
        }

        private async Task Connection_Closed(Exception? arg)
        {
            QueueingState = PlayerState.Disconnected;

            await Task.CompletedTask;
        }

        public async Task StartConnection()
        {
            await _connection.StartAsync();
        }

        public async Task<bool> JoinQueueAsync(IW4MServer server, string playerName)
        {
            try
            {
                _logger.LogDebug("Joining server queue...");
                if (_connection.State is not HubConnectionState.Connected)
                {
                    await StartConnection();
                }

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
    }
}
