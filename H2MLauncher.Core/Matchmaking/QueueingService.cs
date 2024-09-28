using System.Net;

using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Game.Models;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities.SignalR;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Matchmaking;

public sealed class QueueingService : HubClient<IMatchmakingHub>, IQueueingClient, IAsyncDisposable
{
    private readonly IGameCommunicationService _gameCommunicationService;
    private readonly IGameDetectionService _gameDetectionService;
    private readonly IErrorHandlingService _errorHandlingService;

    private readonly IEndpointResolver _endpointResolver;
    private readonly OnlineServiceManager _onlineServiceManager;

    private readonly IOptionsMonitor<H2MLauncherSettings> _options;
    private readonly ILogger<QueueingService> _logger;

    private JoinServerInfo? _queuedServer = null;
    private readonly Dictionary<ServerConnectionDetails, string> _privatePasswords = [];
    private bool _hasSeenConnecting = false;

    public JoinServerInfo? QueuedServer => _queuedServer;
    public int QueuePosition { get; private set; }
    public int TotalPlayersInQueue { get; private set; }


    public event Action<int, int>? QueuePositionChanged;
    public event Action<IServerConnectionDetails>? Joining;
    public event Action<IServerConnectionDetails>? Joined;
    public event Action<IServerConnectionDetails>? JoinFailed;

    public QueueingService(
        ILogger<QueueingService> logger,
        IGameCommunicationService gameCommunicationService,
        IOptionsMonitor<H2MLauncherSettings> options,
        IErrorHandlingService errorHandlingService,
        IGameDetectionService gameDetectionService,
        IEndpointResolver endpointResolver, 
        OnlineServiceManager onlineServiceManager,
        HubConnection hubConnection) : base(hubConnection)
    {
        _logger = logger;
        _options = options;
        _gameCommunicationService = gameCommunicationService;
        _endpointResolver = endpointResolver;
        _onlineServiceManager = onlineServiceManager;
        _gameDetectionService = gameDetectionService;
        _errorHandlingService = errorHandlingService;

        gameCommunicationService.GameStateChanged += GameCommunicationService_GameStateChanged;
        gameCommunicationService.Stopped += GameCommunicationService_Stopped;
        onlineServiceManager.StateChanged += OnlineServiceManager_StateChanged;

        hubConnection.Register<IQueueingClient>(this);
    }

    protected override IMatchmakingHub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken)
    {
        return hubConnection.CreateHubProxy<IMatchmakingHub>(hubCancellationToken);
    }

    private void OnlineServiceManager_StateChanged(PlayerState oldState, PlayerState newState)
    {
        if (newState is PlayerState.Disconnected or PlayerState.Joined or PlayerState.Connected)
        {
            _queuedServer = null;
            _privatePasswords.Clear();
        }
    }

    #region RPC Handlers
    async Task<bool> IQueueingClient.NotifyJoin(JoinServerInfo serverInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received 'NotifyJoin' with {ip} and {port}", serverInfo.Ip, serverInfo.Port);

        if (_onlineServiceManager.State is not PlayerState.Queued)
        {
            _logger.LogWarning("Invalid state: expected Queued but state was {state}", _onlineServiceManager.State);
            return false;
        }

        _queuedServer = serverInfo;

        Joining?.Invoke(_queuedServer);

        string? password = serverInfo.Password ?? _privatePasswords.GetValueOrDefault((serverInfo.Ip, serverInfo.Port));
        JoinServerResult result = await WeakReferenceMessenger.Default.Send<JoinRequestMessage>(new(serverInfo, password, JoinKind.FromQueue));
        if (result is JoinServerResult.Success)
        {
            _onlineServiceManager.State = PlayerState.Joining;
            return true;
        }

        _logger.LogDebug("Could not join server, setting queueing state back to 'Connected'");
        _onlineServiceManager.State = PlayerState.Connected;

        JoinFailed?.Invoke(_queuedServer);
        return false;
    }

    Task IQueueingClient.OnQueuePositionChanged(int position, int totalPlayersInQueue)
    {
        _logger.LogInformation("Received update queue position {queuePosition}/{queueLength}", position, totalPlayersInQueue);

        QueuePosition = position;
        TotalPlayersInQueue = totalPlayersInQueue;

        QueuePositionChanged?.Invoke(position, totalPlayersInQueue);

        return Task.CompletedTask;
    }

    Task IQueueingClient.OnRemovedFromQueue(DequeueReason reason)
    {
        _onlineServiceManager.State = PlayerState.Connected;

        _logger.LogInformation("Removed from queue. Reason: {reason}", reason);

        if (reason is DequeueReason.Unknown)
        {
            _errorHandlingService.HandleError("Removed from queue due to unknown reason.");
        }
        else if (reason is DequeueReason.MaxJoinAttemptsReached)
        {
            _errorHandlingService.HandleError("Removed from queue: Max join attempts reached.");
        }

        return Task.CompletedTask;
    }

    Task IQueueingClient.OnAddedToQueue(JoinServerInfo serverInfo)
    {
        _logger.LogInformation("OnAddedToQueue({serverInfo})", serverInfo);

        _queuedServer = serverInfo;
        _onlineServiceManager.State = PlayerState.Queued;

        return Task.CompletedTask;
    }

    #endregion

    private async void GameCommunicationService_GameStateChanged(GameState gameState)
    {
        if (_onlineServiceManager.State is not (PlayerState.Joining or PlayerState.Queued))
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
            _onlineServiceManager.State = PlayerState.Joined;
            Joined?.Invoke(queuedServer);
        }
        else if (gameState.IsInMainMenu && _queuedServer is not null && _hasSeenConnecting)
        {
            // something went wrong with joining, we have been connection and now are in the main menu again :(

            if (_onlineServiceManager.State is PlayerState.Joining)
            {
                // we were joining, so that's probably the queued server.
                // tell server we could not join, maybe the server got full in the meantime
                // (only if we were actually joining from the queue, not force joining.
                // Otherwise, we want to stay in the queue because the failed join attempt doesn't count)
                _logger.LogInformation("Could not join server, sending failed join ack...");
                await AcknowledgeJoin(false);

                _onlineServiceManager.State = PlayerState.Queued;
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
        if (_onlineServiceManager.State is PlayerState.Joining or PlayerState.Queued or PlayerState.Matchmaking)
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

    public async Task AcknowledgeJoin(bool joinSuccessful)
    {
        try
        {
            await Hub.JoinAck(joinSuccessful).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending join ack");
        }
    }

    public async Task<bool> JoinQueueAsync(IServerInfo server, IPEndPoint? serverEndpoint, string? privatePassword)
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
            await _onlineServiceManager.StartAllConnections();

            serverEndpoint ??= await _endpointResolver.GetEndpointAsync(server, CancellationToken.None);
            if (serverEndpoint is null)
            {
                _logger.LogDebug("Could not resolve endpoint for {@server}", server);
                return false;
            }

            bool joinedSuccesfully = await Hub.JoinQueue(new(server.Ip, server.Port, server.ServerName));
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
            _onlineServiceManager.State = PlayerState.Queued;

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
            if (Connection.State is HubConnectionState.Connected)
            {
                await Hub.LeaveQueue();
                _onlineServiceManager.State = PlayerState.Connected;
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
        return ValueTask.CompletedTask;
    }
}
