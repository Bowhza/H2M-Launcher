using System.Collections.Concurrent;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR
{
    public class QueueingService
    {
        private readonly ConcurrentDictionary<(string ip, int port), GameServer> _servers = [];
        private readonly ConcurrentDictionary<string, Player> _connectedPlayers = [];
        private readonly GameServerCommunicationService<IServerConnectionDetails> _gameServerCommunicationService;
        private readonly IHubContext<QueueingHub, IClient> _ctx;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QueueingService> _logger;
        private readonly ServerInstanceCache _instanceCache;

        public IEnumerable<GameServer> QueuedServers => _servers.Values;

        /// <summary>
        /// Inactivity timeout until the server processing stops.
        /// </summary>
        private readonly static TimeSpan QueueInactivityIdleTimeout = TimeSpan.FromMinutes(3);

        /// <summary>
        /// The maximum amount of time a player can block the queue since the first time a slot becomes available for him.
        /// </summary>
        private static readonly TimeSpan TotalJoinTimeLimit = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The timeout for a join request after which the player will be removed from the queue;
        /// </summary>
        private static readonly TimeSpan JoinTimeout = TotalJoinTimeLimit / MAX_JOIN_ATTEMPTS;

        /// <summary>
        /// The maximum number of join attempts for a player per queue.
        /// </summary>
        private const int MAX_JOIN_ATTEMPTS = 3;

        private const int QUEUE_PLAYER_LIMIT = 50;

        private static readonly bool ConfirmJoinsWithWebfrontApi = false;

        public QueueingService(
            GameServerCommunicationService<IServerConnectionDetails> gameServerCommunicationService,
            IHubContext<QueueingHub, IClient> ctx,
            IHttpClientFactory httpClientFactory,
            ILogger<QueueingService> logger,
            ServerInstanceCache instanceCache)
        {
            _gameServerCommunicationService = gameServerCommunicationService;
            _ctx = ctx;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _instanceCache = instanceCache;
        }

        /// <summary>
        /// Register a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <param name="playerName">Player name</param>
        /// <returns>Whether sucessfully registered.</returns>
        public Player AddPlayer(string connectionId, string playerName)
        {
            Player player = new()
            {
                Name = playerName,
                ConnectionId = connectionId
            };

            if (_connectedPlayers.TryAdd(connectionId, player))
            {
                _logger.LogInformation("Connected player: {player}", player);
            }

            return player;
        }

        /// <summary>
        /// Unregister a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <returns></returns>
        public Player? RemovePlayer(string connectionId)
        {
            if (!_connectedPlayers.TryRemove(connectionId, out var player))
            {
                return null;
            }

            // clean up
            DequeuePlayer(player, default, DequeueReason.Disconnect, notifyPlayerDequeued: false);

            return player;
        }


        /// <summary>
        /// Handle the case when a player reports that the join failed late (e.g. server full) 
        /// </summary>
        public void OnPlayerJoinFailed(string connectionId)
        {
            if (!_connectedPlayers.TryRemove(connectionId, out var player))
            {
                // not found
                return;
            }

            if (player.State is not PlayerState.Joining || player.Server is null)
            {
                // not joining
                return;
            }

            if (player.JoinAttempts.Count >= MAX_JOIN_ATTEMPTS)
            {
                // max join attempts reached -> remove player from queue 
                DequeuePlayer(player, PlayerState.Connected, DequeueReason.MaxJoinAttemptsReached);
                return;
            }

            if (player.Server.LastServerInfo is not null &&
                player.Server.LastServerInfo.FreeSlots == 0)
            {
                // server was probably full

                // allow retry and keep player in queue
                player.State = PlayerState.Queued;
                player.Server.JoiningPlayerCount--;

                // TODO: reset join attempts?
                // player.JoinAttempts.Clear();
            }

            // otherwise dequeue the player
            // TODO: maybe allow player to stay in queue until max join attempts / time limit reached
            DequeuePlayer(player, PlayerState.Connected, DequeueReason.JoinFailed, notifyPlayerDequeued: false);
        }

        public void OnPlayerJoinConfirmed(string connectionId)
        {
            if (!_connectedPlayers.TryRemove(connectionId, out var player))
            {
                // not found                
                return;
            }

            if (player.State is not PlayerState.Joining || player.Server is null)
            {
                // not joining
                _logger.LogWarning("Could not confirm player join: invalid player state ({player})", player);
                return;
            }

            _logger.LogDebug("Player {player} confirmed join", player);

            DequeuePlayer(player, PlayerState.Joined, DequeueReason.Joined, notifyPlayerDequeued: false);
        }

        private async Task TryJoinPlayer(Player player, GameServer server)
        {
            _logger.LogDebug("Notifying {player} to join server {server}...", player, server);

            CancellationTokenSource cancellation = new(JoinTimeout);
            try
            {
                player.JoinAttempts.Add(DateTimeOffset.Now);

                // notify client to join
                var joinTriggeredSuccessfully = await _ctx.Clients.Client(player.ConnectionId)
                    .NotifyJoin(server.ServerIp, server.ServerPort, cancellation.Token);

                if (joinTriggeredSuccessfully)
                {
                    player.State = PlayerState.Joining;
                    server.JoiningPlayerCount++;

                    _logger.LogDebug("Player {player} triggered join to {server} successfully.", player, server);
                }
                else
                {
                    // remove from queue directly when triggering join failed
                    DequeuePlayer(player, PlayerState.Connected, DequeueReason.JoinFailed, notifyPlayerDequeued: false);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // timeout
                _logger.LogDebug("Timed out while waiting for player {player} to join", player);

                DequeuePlayer(player, PlayerState.Connected, DequeueReason.JoinTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while notifying player {player} to join", player);

                // TODO: what do here? dequeue player? notify him?
                DequeuePlayer(player, PlayerState.Connected, DequeueReason.Unknown);
            }
        }

        private void ConfirmJoinedPlayers(GameServer server)
        {
            _logger.LogDebug("Confirming joined players of {server}", server);

            foreach (var node in server.PlayerQueue.GetNodeSnapshot())
            {
                Player player = node.Value;

                if (player.State is PlayerState.Joining &&
                    server.ActualPlayers.Contains(player.Name))
                {
                    if (server.PlayerQueue.TryRemove(node))
                    {
                        server.JoiningPlayerCount--;
                        player.State = PlayerState.Joined;

                        _logger.LogInformation("Confirmed player {player} on server {server}!", player, server);
                        // yippeeeee
                    }
                    else
                    {
                        _logger.LogWarning("Player {player} is removed from queue but still in joining state", player);
                    }
                }
            };
        }

        private async Task UpdateActualPlayersFromWebfront(GameServer server, CancellationToken cancellationToken)
        {
            // TODO: fetch actual players
            _logger.LogDebug("Fetching actual players of server {server}...", server);

            var serverStatusList = await _instanceCache.TryGetWebfrontStatusList(server.InstanceId, cancellationToken);
            if (serverStatusList.Count == 0)
            {
                // either no webfront or no servers
            }

            var serverStatus = serverStatusList.FirstOrDefault(s => s.ListenAddress == server.ServerIp && s.ListenPort == server.ServerPort);
            if (serverStatus is null)
            {
                _logger.LogDebug("No server status found for server instance {instanceId}", server.InstanceId);

                server.ActualPlayers.Clear();

                foreach (var node in server.PlayerQueue.GetNodeSnapshot())
                {
                    if (node.Value.State is PlayerState.Joining)
                    {
                        Player joiningPlayer = node.Value;

                        // no information about actual players -> assume players are joined
                        if (server.PlayerQueue.TryRemove(node))
                        {
                            server.JoiningPlayerCount--;
                            _logger.LogInformation("Assumed player {player} joined on server {server}!", joiningPlayer, server);
                        }
                    }
                };
                return;
            }

            server.ActualPlayers.Clear();
            server.ActualPlayers.AddRange(serverStatus.Players.Select(p => p.Name));

            _logger.LogDebug("Actual players updated for server {server}", server);

            // confirm all the players that are joined after fetching the actual players                    
            ConfirmJoinedPlayers(server);
        }

        private void CheckJoinTimeout(GameServer server)
        {
            foreach (var player in server.JoiningPlayers)
            {
                if (player.State is not PlayerState.Joining || player.JoinAttempts.Count == 0)
                {
                    continue;
                }

                DateTimeOffset now = DateTimeOffset.Now;

                // check total join time limit since first join attempt for this server
                TimeSpan totalJoinTime = now - player.JoinAttempts[0];

                // check join time timeout of current join attempt
                TimeSpan currentJoinTime = now - player.JoinAttempts[player.JoinAttempts.Count - 1];

                if (totalJoinTime > TotalJoinTimeLimit || currentJoinTime > JoinTimeout)
                {
                    DequeuePlayer(player, PlayerState.Connected, DequeueReason.JoinTimeout);
                }
            }
        }

        private async Task<bool> FetchGameServerInfoAsync(GameServer server, CancellationToken cancellationToken)
        {
            CancellationTokenSource timeoutCts = new CancellationTokenSource(10000);
            CancellationTokenSource linkedCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                _logger.LogDebug("Requesting game server info for {server}...", server);

                GameServerInfo? gameServerInfo = await _gameServerCommunicationService.RequestServerInfoAsync(server, linkedCancellation.Token);
                if (gameServerInfo is null)
                {
                    // could not send request
                    _logger.LogInformation("Could not send server info request");
                    server.LastServerInfo = null;
                    return false;
                }

                // successful
                _logger.LogInformation("Server info retrieved successfully: {gameServerInfo}", gameServerInfo);

                server.LastServerInfo = gameServerInfo;
                server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;

                return true;
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
            {
                // timed out
                _logger.LogWarning("Timed out while requesting server info");
                server.LastServerInfo = null;
                return false;
            }
            finally
            {
                timeoutCts.Dispose();
                linkedCancellation.Dispose();
            }
        }

        private async Task ProcessQueuedPlayers(GameServer server, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogTrace("Processing {numberOfQueuedPlayers} queued ({joiningPlayersCount} joining) players for server {server}",
                    server.PlayerQueue.Count, server.JoiningPlayerCount, server);

                // only need to recheck if players are joining
                // no reserved slots means all players are still queued
                if (ConfirmJoinsWithWebfrontApi && server.JoiningPlayerCount > 0)
                {
                    _logger.LogDebug("{joiningPlayersCount}/{numberOfQueuedPlayers} joining players found, updating actual player list for server {server}",
                        server.JoiningPlayerCount, server.PlayerQueue.Count, server);

                    // fetch actual players from web front and update join state
                    await UpdateActualPlayersFromWebfront(server, cancellationToken);
                }

                // dequeue players that have been joining too long
                CheckJoinTimeout(server);

                // check whether to continue
                if (server.JoiningPlayerCount == server.PlayerQueue.Count)
                {
                    // all players in queue already joining -> nothing to do anymore
                    _logger.LogTrace("All queued players ({numberOfQueuedPlayers}) are currently joining server {server}", server.PlayerQueue.Count, server);
                    return;
                }

                // request the latest game server info
                if (!await FetchGameServerInfoAsync(server, cancellationToken))
                {
                    return;
                }

                // now do the actual check for joining
                await HandlePlayerJoinsAsync(server, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // TODO: should we break the entire loop?
                _logger.LogError(ex, "Error during server process queue. {server}", server);
            }
        }

        private async Task ServerProcessingLoop(GameServer server)
        {
            CancellationToken cancellationToken = server.ProcessingCancellation.Token;

            _logger.LogInformation("Started processing loop for server {server}", server);

            server.ProcessingState = QueueProcessingState.Running;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // only need to do something when player is queued
                    var hasQueuedPlayers = server.PlayerQueue.Count > 0;
                    if (hasQueuedPlayers)
                    {
                        // start the delay
                        Task delayTask = Task.Delay(1000, cancellationToken);

                        await ProcessQueuedPlayers(server, cancellationToken);

                        await delayTask;
                    }
                    else
                    {
                        server.ProcessingState = QueueProcessingState.Idle;
                        _logger.LogInformation("No players in queue for server {server}, switched to idle state", server);

                        // wait for the timeout period to see if new players join the queue
                        Task idleTimeoutTask = Task.Delay(QueueInactivityIdleTimeout, cancellationToken);

                        // if new players arrive during this time, break out of the waiting period
                        await Task.WhenAny(idleTimeoutTask, server.PlayersAvailable.WaitOneAsync());

                        // if the delay completed (i.e., timeout passed without new players), stop the processing loop
                        if (idleTimeoutTask.IsCompleted)
                        {
                            server.ProcessingState = QueueProcessingState.Stopped;
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Server queue processing loop canceled for {server}", server);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during server queue processing loop. {server}", server);
                server.ProcessingState = QueueProcessingState.Stopped;
            }
            finally
            {
                _logger.LogInformation("Processing loop stopped for server {server}", server);
            }
        }


        private async Task HandlePlayerJoinsAsync(GameServer server, CancellationToken cancellationToken)
        {
            if (server.LastServerInfo is null)
            {
                return;
            }

            _logger.LogDebug("Server {server} has {freeSlots} free, {reservedSlots} reserved slots",
                           server, server.LastServerInfo.FreeSlots, server.JoiningPlayerCount);

            // get the number of available slots (not reserved by joining players)
            int nonReservedFreeSlots = server.LastServerInfo.FreeSlots - server.JoiningPlayerCount;

            _logger.LogDebug("Can join up to {numberOfPeople} people to {server}",
                nonReservedFreeSlots, server);


            // try to join as many players as slots available
            int joinedPlayers = 0;
            foreach (Player player in server.QueuedPlayers)
            {
                if (player.State is PlayerState.Queued && joinedPlayers < nonReservedFreeSlots)
                {
                    await TryJoinPlayer(player, server);
                    joinedPlayers++;
                }
                else
                {
                    // unknown player state
                }
            }
        }

        private readonly SemaphoreSlim _serverLock = new(1, 1);

        #region CRUD stuff for servers

        public async Task HaltQueue(string serverIp, int serverPort)
        {
            if (!_servers.TryGetValue((serverIp, serverPort), out GameServer? server))
            {
                // no queue for this server
                return;
            }

            if (server.ProcessingState is QueueProcessingState.Paused)
            {
                // already halted
                return;
            }

            await _serverLock.WaitAsync();
            try
            {
                if (server.ProcessingState is QueueProcessingState.Paused)
                {
                    return;
                }

                // cancel processing loop and set state
                await server.ProcessingCancellation.CancelAsync();
                server.ProcessingState = QueueProcessingState.Paused;

                // await task to catch potential exceptions
                var processingTask = server.ProcessingTask;
                if (processingTask is not null)
                {
                    await processingTask;
                }

                _logger.LogInformation("Server queue for {server} has been halted with {numberOfQueuedPlayers} players.",
                    server, server.PlayerQueue.Count);
            }
            finally
            {
                _serverLock.Release();
            }
        }

        public async Task<int> ClearQueue(string serverIp, int serverPort)
        {
            if (!_servers.TryGetValue((serverIp, serverPort), out GameServer? server))
            {
                // no queue for this server
                return 0;
            }

            if (server.PlayerQueue.Count == 0)
            {
                // already empty
                return 0;
            }

            await _serverLock.WaitAsync();
            try
            {
                List<Player> queuedPlayers = server.PlayerQueue.ToList();
                server.PlayerQueue.Clear();
                server.JoiningPlayerCount = 0;

                foreach (var player in queuedPlayers)
                {
                    if (player.State is PlayerState.Queued or PlayerState.Joining)
                    {
                        player.State = PlayerState.Connected;
                    }

                    await NotifyPlayerDequeued(player, DequeueReason.Unknown);
                }

                _logger.LogInformation("Server queue for {server} has been cleared ({numberOfQueuedPlayers} players removed).",
                    server, queuedPlayers.Count);

                return queuedPlayers.Count;
            }
            finally
            {
                _serverLock.Release();
            }
        }

        public void CleanupZombieQueues()
        {
            foreach (GameServer server in _servers.Values)
            {
                if (server.ProcessingState is QueueProcessingState.Stopped &&
                    (server.ProcessingTask is null || server.ProcessingTask.IsCompleted))
                {
                    TryCleanupServer(server);
                }
            }
        }

        private void TryCleanupServer(GameServer gameServer)
        {
            if (gameServer.ProcessingState is QueueProcessingState.Running or QueueProcessingState.Paused ||
                gameServer.PlayerQueue.Count != 0)
            {
                _logger.LogWarning("Tried to cleanup active server {server}", gameServer);
                return;
            }

            if (_servers.TryRemove((gameServer.ServerIp, gameServer.ServerPort), out GameServer? server))
            {
                _logger.LogInformation("Removed queued server {server} after {timeSinceSpawned}",
                    server, DateTimeOffset.Now - server.SpawnDate);
            }
        }

        public async Task DestroyQueue(string serverIp, int serverPort, bool remove)
        {
            if (!_servers.TryGetValue((serverIp, serverPort), out GameServer? server))
            {
                // no queue for this server
                return;
            }

            if (server.ProcessingState is not QueueProcessingState.Running)
            {
                // already halted
                return;
            }

            await _serverLock.WaitAsync();
            try
            {
                if (server.ProcessingState is not QueueProcessingState.Running)
                {
                    return;
                }

                // cancel processing loop and set state
                await server.ProcessingCancellation.CancelAsync();
                server.ProcessingState = QueueProcessingState.Stopped;

                // await task to catch potential exceptions
                var processingTask = server.ProcessingTask;
                if (processingTask is not null)
                {
                    await processingTask;
                }

                int numberOfQueuedPlayers = 0;
                foreach (Player player in server.PlayerQueue)
                {
                    if (player.State is PlayerState.Queued or PlayerState.Joining)
                    {
                        numberOfQueuedPlayers++;
                        DequeuePlayer(player, PlayerState.Connected, DequeueReason.Unknown, true);
                    }
                }

                _logger.LogInformation("Server queue for {server} has been destroyed with {numberOfQueuedPlayers} players.",
                    server, numberOfQueuedPlayers);

                if (remove)
                {
                    TryCleanupServer(server);
                }
            }
            finally
            {
                _serverLock.Release();
            }
        }

        private async Task<GameServer> SpawnServerQueue(string instanceId, string serverIp, int serverPort)
        {
            await _serverLock.WaitAsync();
            try
            {
                if (_servers.TryGetValue((serverIp, serverPort), out GameServer? server))
                {
                    if (server.ProcessingState is QueueProcessingState.Stopped)
                    {
                        // restart processing queued players
                        server.ProcessingCancellation = new();
                        server.ProcessingTask = ServerProcessingLoop(server);
                    }

                    return server;
                }

                // server does not have a queue yet
                server = new(instanceId)
                {
                    ServerIp = serverIp,
                    ServerPort = serverPort
                };

                if (!_servers.TryAdd((serverIp, serverPort), server))
                {
                    // in the meantime the server got added (TODO: locking)
                    throw new Exception("invalid state");
                }

                // start processing queued players
                server.ProcessingCancellation = new();
                server.ProcessingTask = ServerProcessingLoop(server);

                return server;
            }
            finally
            {
                _serverLock.Release();
            }
        }

        #endregion

        public async Task<bool> JoinQueue(string serverIp, int serverPort, string connectionId, string instanceId)
        {
            if (!_connectedPlayers.TryGetValue(connectionId, out var player))
            {
                // unknown player
                return false;
            }

            if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                return false;
            }

            if (!_servers.TryGetValue((serverIp, serverPort), out GameServer? server))
            {
                server = await SpawnServerQueue(instanceId, serverIp, serverPort);
            }

            if (server.PlayerQueue.Contains(player))
            {
                // player is already queued in server
                return false;
            }

            if (server.PlayerQueue.Count > QUEUE_PLAYER_LIMIT)
            {
                // queue limit
                return false;
            }

            // queue the player
            player.State = PlayerState.Queued;
            player.QueuedAt = DateTimeOffset.Now;
            player.Server = server;
            player.JoinAttempts = [];
            server.PlayerQueue.Enqueue(player);

            // signal available
            server.PlayersAvailable.Set();

            await NotifyPlayerQueuePositions(server);

            return true;
        }

        public void LeaveQueue(string connectionId)
        {
            if (!_connectedPlayers.TryGetValue(connectionId, out var player))
            {
                // unknown player
                return;
            }

            DequeuePlayer(player, PlayerState.Connected, DequeueReason.UserLeave);
        }

        private void DequeuePlayer(Player player, PlayerState newState, DequeueReason reason, bool notifyPlayerDequeued = true)
        {
            if (player.State is not (PlayerState.Queued or PlayerState.Joining) || player.Server is null)
            {
                return;
            }

            _logger.LogDebug("Dequeueing player {player} from server {server}, reason '{reason}'",
                player, player.Server, reason);

            if (!player.Server.PlayerQueue.Remove(player))
            {
                // invalid state, player expected in queue
                _logger.LogWarning("Invalid state, expected player {player} with joining state to be in queue", player);
                return;
            }

            if (player.State is PlayerState.Joining)
            {
                player.Server.JoiningPlayerCount--;
            }

            player.State = newState;

            // TODO
            _ = NotifyPlayerQueuePositions(player.Server);

            if (reason is DequeueReason.UserLeave or DequeueReason.Disconnect || !notifyPlayerDequeued)
            {
                return;
            }

            _ = NotifyPlayerDequeued(player, reason);
        }

        private async Task NotifyPlayerQueuePositions(GameServer server)
        {
            // Notify other players of their updated queue position
            int queuePosition = 0;
            int queueLength = server.PlayerQueue.Count;

            foreach (Player player in server.PlayerQueue)
            {
                try
                {
                    await _ctx.Clients.Client(player.ConnectionId).QueuePositionChanged(++queuePosition, queueLength);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while notifying player {player} of queue position", player);
                }
            }
        }

        private async Task NotifyPlayerDequeued(Player player, DequeueReason reason)
        {
            try
            {
                await _ctx.Clients.Client(player.ConnectionId).RemovedFromQueue(reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify player dequeued. {player}", player);
            }
        }
    }
}
