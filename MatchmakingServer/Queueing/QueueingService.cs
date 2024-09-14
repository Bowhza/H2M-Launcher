using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace MatchmakingServer.Queueing
{
    public class QueueingService
    {
        private readonly ServerStore _serverStore;

        private readonly GameServerCommunicationService<GameServer> _gameServerCommunicationService;
        private readonly IHubContext<QueueingHub, IClient> _ctx;
        private readonly ILogger<QueueingService> _logger;
        private readonly ServerInstanceCache _instanceCache;
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;
        private readonly IOptionsMonitor<QueueingSettings> _queueingSettings;
        private readonly SemaphoreSlim _serverLock = new(1, 1);

        public IEnumerable<GameServer> QueuedServers => _serverStore.Servers.Values;

        private QueueingSettings QueueingSettings => _queueingSettings.CurrentValue;

        /// <summary>
        /// Inactivity timeout until the server processing stops.
        /// </summary>
        private TimeSpan QueueInactivityIdleTimeout => TimeSpan.FromSeconds(QueueingSettings.QueueInactivityIdleTimeoutInS);

        /// <summary>
        /// The maximum amount of time a player can block the queue since the first time a slot becomes available for him.
        /// </summary>
        private TimeSpan TotalJoinTimeLimit => TimeSpan.FromSeconds(QueueingSettings.TotalJoinTimeLimitInS);

        /// <summary>
        /// The timeout for a join request after which the player will be removed from the queue;
        /// </summary>
        private TimeSpan JoinTimeout => TimeSpan.FromSeconds(QueueingSettings.JoinTimeoutInS);


        public QueueingService(
            GameServerCommunicationService<GameServer> gameServerCommunicationService,
            IHubContext<QueueingHub, IClient> ctx,
            ILogger<QueueingService> logger,
            ServerInstanceCache instanceCache,
            IOptionsMonitor<ServerSettings> serverSettings,
            IOptionsMonitor<QueueingSettings> queueingSettings,
            ServerStore serverStore)
        {
            _gameServerCommunicationService = gameServerCommunicationService;
            _ctx = ctx;
            _logger = logger;
            _instanceCache = instanceCache;
            _serverSettings = serverSettings;
            _queueingSettings = queueingSettings;
            _serverStore = serverStore;
        }


        /// <summary>
        /// Handle the case when a player reports that the join failed late (e.g. server full) 
        /// </summary>
        public void OnPlayerJoinFailed(Player player)
        {
            if (player.State is not PlayerState.Joining || player.Server is null)
            {
                // not joining
                return;
            }

            _logger.LogDebug("Player {player} reported join failed", player);

            if (player.JoinAttempts.Count >= QueueingSettings.MaxJoinAttempts)
            {
                // max join attempts reached -> remove player from queue 
                _logger.LogTrace("Max join attempts reached, dequeueing player {player}...", player);
                DequeuePlayer(player, PlayerState.Connected, DequeueReason.MaxJoinAttemptsReached);
                return;
            }

            // allow retry and keep player in queue
            player.State = PlayerState.Queued;
            player.Server.JoiningPlayerCount--;

            _logger.LogDebug("Moved player {player} back to queue", player);

            if (player.Server.LastServerInfo is not null &&
                player.Server.LastServerInfo.FreeSlots == 0)
            {
                // server was probably full
                if (QueueingSettings.ResetJoinAttemptsWhenServerFull)
                {
                    // reset join attempts / timer to make it more fair?
                    _logger.LogTrace("Last server info reported 0 free slots, resetting joing attempts for {player}", player);
                    player.JoinAttempts.Clear();
                }

                return;
            }

            // TODO: maybe allow player to stay in queue until max join attempts / time limit reached
        }

        public void OnPlayerJoinConfirmed(Player player)
        {
            if (player.State is not (PlayerState.Joining or PlayerState.Queued) || player.Server is null)
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
                    .NotifyJoin(server.GetActualIpAddress(), server.ServerPort, cancellation.Token);

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
                        player.QueuedAt = null;

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
            _logger.LogDebug("Fetching actual players of server {server}...", server);

            IReadOnlyList<IW4MServerStatus> serverStatusList = await _instanceCache.TryGetWebfrontStatusList(server.InstanceId, cancellationToken);

            IW4MServerStatus? serverStatus = serverStatusList.FirstOrDefault(s =>
                s.ListenAddress == server.ServerIp &&
                s.ListenPort == server.ServerPort);

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
                _logger.LogTrace("Requesting game server info for {server}...", server);

                GameServerInfo? gameServerInfo = await _gameServerCommunicationService.GetInfoAsync(server, linkedCancellation.Token);
                if (gameServerInfo is null)
                {
                    // could not send request
                    _logger.LogDebug("Could not send server info request");
                    server.LastServerInfo = null;
                    return false;
                }

                // successful
                _logger.LogTrace("Server info retrieved successfully: {gameServerInfo}", gameServerInfo);

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
                _logger.LogDebug("Processing {numberOfQueuedPlayers} queued ({joiningPlayersCount} joining) players for server {server}",
                    server.PlayerQueue.Count, server.JoiningPlayerCount, server);

                // only need to recheck if players are joining
                // no reserved slots means all players are still queued
                if (QueueingSettings.ConfirmJoinsWithWebfrontApi && server.JoiningPlayerCount > 0)
                {
                    _logger.LogDebug("{joiningPlayersCount}/{numberOfQueuedPlayers} joining players found, updating actual player list for server {server}",
                        server.JoiningPlayerCount, server.PlayerQueue.Count, server);

                    // fetch actual players from web front and update join state
                    await UpdateActualPlayersFromWebfront(server, cancellationToken);
                }

                // dequeue players that have been joining too long
                CheckJoinTimeout(server);

                if (server.PlayerQueue.Count == 0)
                {
                    return;
                }

                // check whether to continue
                if (!QueueingSettings.ResetJoinAttemptsWhenServerFull && server.JoiningPlayerCount == server.PlayerQueue.Count)
                {
                    // all players in queue already joining -> nothing to do anymore
                    _logger.LogDebug("All queued players ({numberOfQueuedPlayers}) are currently joining server {server}", server.PlayerQueue.Count, server);
                    return;
                }

                // request the latest game server info
                if (!await FetchGameServerInfoAsync(server, cancellationToken))
                {
                    return;
                }

                // check whether to continue
                int queuedPlayerCount = server.PlayerQueue.Count;
                if (server.JoiningPlayerCount == queuedPlayerCount)
                {
                    // all players in queue already joining -> nothing to do anymore
                    _logger.LogDebug("All queued players ({numberOfQueuedPlayers}) are currently joining server {server}", queuedPlayerCount, server);
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

            server.ProcessingState = QueueProcessingState.Idle;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // only need to do something when player is queued
                    var hasQueuedPlayers = server.PlayerQueue.Count > 0;
                    if (hasQueuedPlayers)
                    {
                        server.ProcessingState = QueueProcessingState.Running;

                        // start the delay
                        Task delayTask = Task.Delay(1000, cancellationToken);

                        await ProcessQueuedPlayers(server, cancellationToken);

                        await delayTask;
                    }
                    else
                    {
                        server.ProcessingState = QueueProcessingState.Idle;
                        server.PlayersAvailable.Reset();

                        _logger.LogInformation("No players in queue for server {server}, switched to idle state", server);

                        // wait for the timeout period to see if new players join the queue
                        Task idleTimeoutTask = Task.Delay(QueueInactivityIdleTimeout, cancellationToken);

                        // if new players arrive during this time, break out of the waiting period
                        await Task.WhenAny(idleTimeoutTask, server.PlayersAvailable.WaitAsync());

                        // if the delay completed (i.e., timeout passed without new players), stop the processing loop
                        if (idleTimeoutTask.IsCompleted)
                        {
                            _logger.LogDebug("Idle timeout reached, stopping processing loop for {server}", server);
                            server.ProcessingState = QueueProcessingState.Stopped;

                            if (QueueingSettings.CleanupServerWhenStopped)
                            {
                                TryCleanupServer(server);
                            }
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

                if (QueueingSettings.CleanupServerWhenStopped)
                {
                    TryCleanupServer(server);
                }
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

            _logger.LogDebug("Server {server} has {freeSlots} free, {privilegedSlots} privileged, {reservedSlots} reserved slots",
                           server, server.LastServerInfo.FreeSlots, server.PrivilegedSlots, server.JoiningPlayerCount);

            // get the number of available slots (not reserved by joining players or privileged slots)
            int nonReservedFreeSlots = Math.Max(server.LastServerInfo.FreeSlots - server.UnavailableSlots, 0);
            if (nonReservedFreeSlots == 0)
            {
                _logger.LogDebug("No slot available on server {server}", server);
                return;
            }

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
                    _logger.LogWarning("Invalid player state for queued player {player}", player);
                }
            }
        }

        #region CRUD stuff for servers

        public Task HaltQueue(string serverIp, int serverPort)
        {
            if (!_serverStore.Servers.TryGetValue((serverIp, serverPort), out GameServer? server))
            {
                // no queue for this server
                return Task.CompletedTask;
            }

            return HaltQueue(server);
        }

        public async Task HaltQueue(GameServer server)
        {
            if (server.ProcessingState is QueueProcessingState.Paused)
            {
                // already halted
                return;
            }

            _logger.LogDebug("Halting queue for server {server}", server);

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

        public async Task<int> ClearQueue(GameServer server)
        {
            if (server.PlayerQueue.Count == 0)
            {
                // already empty
                return 0;
            }

            _logger.LogDebug("Clearing queue for server {server}", server);

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
                        player.QueuedAt = null;
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

        public Task<int> ClearQueue(string serverIp, int serverPort)
        {
            if (!_serverStore.Servers.TryGetValue((serverIp, serverPort), out GameServer? server))
            {
                // no queue for this server
                return Task.FromResult(0);
            }

            return ClearQueue(server);
        }

        public int CleanupZombieQueues()
        {
            _logger.LogDebug("Cleaning up zombie queues...");
            int numQueuesRemoved = 0;

            foreach (GameServer server in _serverStore.Servers.Values)
            {
                if (server.ProcessingState is not QueueProcessingState.Stopped ||
                    server.ProcessingTask is not null && !server.ProcessingTask.IsCompleted)
                {
                    continue;
                }

                if (TryCleanupServer(server))
                {
                    numQueuesRemoved++;
                }
            }

            _logger.LogInformation("Removed {numQueuesRemoved} queues.", numQueuesRemoved);
            return numQueuesRemoved;
        }

        private bool TryCleanupServer(GameServer gameServer)
        {
            _logger.LogTrace("Cleaning up server {server}", gameServer);

            if (gameServer.ProcessingState is QueueProcessingState.Running or QueueProcessingState.Paused ||
                gameServer.PlayerQueue.Count != 0)
            {
                _logger.LogWarning("Tried to cleanup active server {server}", gameServer);
                return false;
            }

            if (_serverStore.Servers.Remove((gameServer.ServerIp, gameServer.ServerPort), out GameServer? server))
            {
                _logger.LogInformation("Removed queued server {server} after {timeSinceSpawned}",
                    server, DateTimeOffset.Now - server.SpawnDate);

                return true;
            }

            return false;
        }

        public async Task DestroyQueue(GameServer server, bool remove = true)
        {
            if (server.ProcessingState is QueueProcessingState.Stopped && !remove)
            {
                // already halted
                return;
            }

            await _serverLock.WaitAsync();
            try
            {
                if (server.ProcessingState is QueueProcessingState.Stopped && !remove)
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

        #endregion

        public void StartQueue(GameServer server)
        {
            if (server.ProcessingState is QueueProcessingState.Stopped)
            {
                // (re)start processing queued players
                server.ProcessingCancellation = new();
                server.ProcessingTask = ServerProcessingLoop(server);
            }
        }

        public async Task<bool> JoinQueue(GameServer server, Player player)
        {
            if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                _logger.LogWarning("Cannot join queue for {server}, player {player} already queued", server, player);
                return false;
            }

            if (server.PlayerQueue.Contains(player))
            {
                // player is already queued in server
                _logger.LogWarning("Player {player} already queued on this server {server}.", player, server);
                return false;
            }

            if (server.PlayerQueue.Count > QueueingSettings.QueuePlayerLimit)
            {
                // queue limit
                _logger.LogDebug("Cannot join queue for {server}, queue limit ({playersInQueue}/{queueLimit}) reached.",
                    server, server.PlayerQueue.Count, QueueingSettings.QueuePlayerLimit);
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

            StartQueue(server);

            _logger.LogDebug("Player {player} queued on {server}", player, server);

            await NotifyPlayerQueuePositions(server);

            return true;
        }

        public Task<bool> JoinQueue(string serverIp, int serverPort, Player player, string instanceId)
        {
            if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                _logger.LogWarning("Cannot join queue for {serverIp}:{serverPort}, player {player} already queued",
                    serverIp, serverPort, player);
                return Task.FromResult(false);
            }

            GameServer server = _serverStore.GetOrAddServer(serverIp, serverPort, instanceId);

            return JoinQueue(server, player);
        }

        public void LeaveQueue(Player player, bool disconnected = false)
        {
            DequeuePlayer(player,
                disconnected ? PlayerState.Disconnected : PlayerState.Connected,
                disconnected ? DequeueReason.UserLeave : DequeueReason.Disconnect);
        }

        private void DequeuePlayer(Player player, PlayerState newState, DequeueReason reason, bool notifyPlayerDequeued = true)
        {
            if (player.State is not (PlayerState.Queued or PlayerState.Joining) || player.Server is null)
            {
                _logger.LogDebug("Cannot dequeue {player}: not in queue", player);
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
            player.QueuedAt = null;

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
