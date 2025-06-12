using System.Collections.Concurrent;
using System.Reactive.Linq;

using AsyncKeyedLock;

using ConcurrentCollections;

using FxKit.Extensions;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Social;

using MatchmakingServer.SignalR;

namespace MatchmakingServer.Social;

/// <summary>
/// Tracks the gamer servers clients are playing on by verifying players through server status responses.
/// </summary>
public class PlayerServerTrackingService : BackgroundService, IPlayerServerTrackingService
{
    private const int CHECKING_INTERVAL_MS = 3000;

    /// <summary>
    /// Whether to stop tracking players and remove them from the server when client reports no connected server.
    /// </summary>
    private static readonly bool RemovePlayerOnClientUpdate = true;

    /// <summary>
    /// The maximum time to track a player after disconnect or reporting no connected server.
    /// </summary>
    private static readonly TimeSpan MaxTrackingTimeAfterDisconnect = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The maximum age of a cached server status response used for checking tracked servers.
    /// </summary>
    private static readonly TimeSpan TrackingServerStatusMaxAge = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Timeout after which to remove a server from tracking if it does not respond to status requests.
    /// </summary>
    private static readonly TimeSpan ServerStatusTimeout = TimeSpan.FromMinutes(1);

    private readonly IMasterServerService _masterServerService;
    private readonly GameServerService _gameServerService;
    private readonly ServerStore _serverStore;
    private readonly ILogger<PlayerServerTrackingService> _logger;

    private readonly SemaphoreSlim _serverCheckLock = new(1, 1);
    private readonly AsyncKeyedLocker<string> _playerLock = new();

    private readonly ConcurrentHashSet<GameServer> _trackedGameServers = [];
    private readonly ConcurrentDictionary<Player, TrackedPlayerInfo> _trackedPlayers = [];

    public PlayerServerTrackingService(
        IMasterServerService masterServerService,
        GameServerService gameServerService,
        ServerStore serverStore,
        ILogger<PlayerServerTrackingService> logger)
    {
        _masterServerService = masterServerService;
        _gameServerService = gameServerService;
        _serverStore = serverStore;
        _logger = logger;
    }

    public IReadOnlyCollection<GameServer> TrackedServers => _trackedGameServers;
    public IReadOnlyCollection<Player> TrackedPlayers => new ReadOnlyCollectionWrapper<Player>(_trackedPlayers.Keys);

    public event Action<GameServer>? ServerTimeout;
    public event Action<PlayerLeftEventArgs>? PlayerLeftServer;
    public event Action<Player, GameServer>? PlayerJoinedServer; //TODO: consume and notify each other of new recent

    private readonly record struct TrackedPlayerInfo
    {
        public ConnectedServerInfo? LastConnectionInfo { get; init; }

        public DateTimeOffset LastConnectionInfoTimestamp { get; init; }

        public GameServer MatchedServer { get; init; }

        public int MatchConfidenceScore { get; init; }
    }

    public class PlayerLeftEventArgs : EventArgs
    {
        public required GameServer Server { get; init; }
        public required Player Player { get; init; }
        public required DateTimeOffset JoinDate { get; init; }
        public required DateTimeOffset LeaveDate { get; init; }
        public bool IsServerTimeout { get; init; }
    }

    /// <summary>
    /// Main loop that monitors the servers and players.
    /// </summary>    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            while (_trackedGameServers.Count == 0)
            {
                // wait for servers to be added
                await Task.Delay(1000, stoppingToken);
            }

            await CheckServers(stoppingToken);
            await Task.Delay(CHECKING_INTERVAL_MS, stoppingToken);
        }
    }


    /// <summary>
    /// Verifies whether each monitored server is still online and the known players are still on the server.
    /// </summary>
    public async Task CheckServers(CancellationToken cancellationToken)
    {
        await _serverCheckLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Checking tracked servers ({numTrackedServers}) and players ({numTrackedPlayers})...",
                _trackedGameServers.Count, _trackedPlayers.Count);

            IAsyncEnumerable<GameServer> refreshedServers = _gameServerService.GetServerStatusAsync(
                gameServers: _trackedGameServers,
                maxAge: TrackingServerStatusMaxAge,
                cancellationToken: cancellationToken);

            await foreach (GameServer server in refreshedServers.ConfigureAwait(false))
            {
                if (server.LastStatusResponse is null) continue;

                // Verify server is still online
                if (server.LastServerStatusTimestamp < DateTimeOffset.Now.Subtract(ServerStatusTimeout))
                {
                    // Server is not responding with status anymore -> remove
                    _logger.LogDebug("Declaring server {server} timed out", server);
                    await RemoveServer(server, true);
                    ServerTimeout?.Invoke(server);
                    continue;
                }

                if (server.KnownPlayers.Count == 0)
                {
                    _logger.LogDebug("Removing tracked server {server} because of no players.", server);
                    await RemoveServer(server, false);
                    continue;
                }

                // Verify players still on server
                foreach (Player player in server.KnownPlayers.Keys)
                {
                    if (!server.LastStatusResponse.Players.Any(p => p.PlayerName == player.Name))
                    {
                        // Player not on server anymore (TODO: check what happens with name changes)
                        await RemovePlayerFromServer(player, server);
                        StopTrackingPlayer(player);
                        continue;
                    }

                    if (player.GameStatus is not GameStatus.InMatch || player.State is PlayerState.Disconnected)
                    {
                        // We are tracking this player despite the client reporting not being in a match
                        // or after he disconnected, so check if we should stop.

                        if (!_trackedPlayers.TryGetValue(player, out TrackedPlayerInfo trackedPlayerInfo))
                        {
                            // Player is not tracked anymore? Remove him for consistency
                            await RemovePlayerFromServer(player, server);
                            continue;
                        }

                        TimeSpan maxTrackingTimeAdjusted = MaxTrackingTimeAfterDisconnect * trackedPlayerInfo.MatchConfidenceScore;

                        if (trackedPlayerInfo.LastConnectionInfoTimestamp < DateTimeOffset.Now.Subtract(maxTrackingTimeAdjusted))
                        {
                            // Max tracking time exceeded so removing the player because we do not have enough confidence to say
                            // he is still on there.
                            await RemovePlayerFromServer(player, server, isTimeout: true);
                            StopTrackingPlayer(player);
                        }
                    }
                }

                // Now handle normal updates of server state
            }
        }
        finally
        {
            _serverCheckLock.Release();
        }
    }

    /// <summary>
    /// Handles server connection updates sent by the client for the given <paramref name="player"/> by finding a matching server
    /// and starting or stopping tracking.
    /// </summary>
    /// <param name="player">The player associated with the connection update.</param>
    /// <param name="connectedServerInfo">The info about the connected server or <see langword="null"/> if disconnected.</param>
    public async Task HandlePlayerConnectionUpdate(Player player, ConnectedServerInfo? connectedServerInfo, CancellationToken cancellationToken)
    {
        DateTimeOffset timestamp = DateTimeOffset.Now;

        _logger.LogDebug("Handling server connection update for player {player}: {connectedServerInfo}",
            player, connectedServerInfo);

        if (connectedServerInfo is null)
        {
            // Player is not connected to any server -> remove or detect with next check
            if (RemovePlayerOnClientUpdate)
            {
                await RemovePlayerFromCurrentServer(player);
            }

            TryUpdateTrackedPlayerInfo(player, (_, prevInfo) => prevInfo with
            {
                LastConnectionInfo = null,
                LastConnectionInfoTimestamp = timestamp
            });

            return;
        }


        // Find a server matching the info
        (GameServer? matchingServer, int confidence) = await MatchServer(player, connectedServerInfo, cancellationToken);
        if (matchingServer is null)
        {
            // Since the player is connected to a server we cannot identify,
            // remove them from the current server he is tracked on
            await RemovePlayerFromCurrentServer(player);

            // Also remove him from the tracking, as it is very unlike now he is still on the same server as before.
            StopTrackingPlayer(player);
            return;
        }

        // Track the player on this matching server
        if (!await AddPlayerToServer(player, matchingServer, migrate: true))
        {
            return;
        }

        _trackedGameServers.Add(matchingServer);

        _trackedPlayers[player] = new TrackedPlayerInfo()
        {
            LastConnectionInfo = connectedServerInfo,
            LastConnectionInfoTimestamp = timestamp,
            MatchedServer = matchingServer,
            MatchConfidenceScore = confidence,
        };

        _logger.LogInformation("Tracking player {player} connected on server {server}.", player, matchingServer);
    }

    /// <summary>
    /// Finds a matching server from the <paramref name="connectedServerInfo"/> sent by the client for the given <paramref name="player"/>
    /// using a heuristic approach that involves checking the servers info and status responses and comparing the server name and connected player names.
    /// </summary>
    /// <returns>The matching server, if found.</returns>
    internal async Task<(GameServer? Server, int Confidence)> MatchServer(
        Player player,
        ConnectedServerInfo connectedServerInfo,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<ServerConnectionDetails> servers = await _masterServerService.GetServersAsync(CancellationToken.None);
        IEnumerable<ServerConnectionDetails> serversMatchingIp = servers.Where(s => s.Ip == connectedServerInfo.Ip);

        //if (connectedServerInfo.PortGuess.HasValue)
        //{
        //    var serverMatchingPort = serversMatchingIp.FirstOrDefault(s => s.Port == connectedServerInfo.PortGuess.Value);
        //    if (serverMatchingPort is not null)
        //    {
        //        GameServer gameServer = _serverStore.GetOrAddServer(
        //            serverMatchingPort.Ip,
        //            serverMatchingPort.Port,
        //            connectedServerInfo.ServerName);

        //        GameServerStatus? status = await _gameServerService.GetServerStatusAsync(gameServer, cancellationToken: cancellationToken);
        //        if (status is not null && status.Players.Any(p => p.PlayerName.Equals(player.Name)))
        //        {
        //            // match
        //            _logger.LogInformation("Found matching server {server} for player {player} with confidence score of {matchConfidence}",
        //                gameServer, player, 3);
        //            return (gameServer, 3);
        //        }
        //    }
        //}

        List<GameServer> gameServersMatchingIp = serversMatchingIp.Select(server =>
        {
            return _serverStore.GetOrAddServer(
                server.Ip,
                server.Port,
                connectedServerInfo.ServerName);
        }).ToList();

        List<GameServer> respondingServers = await _gameServerService.RefreshInfoAndStatusAsync(
            gameServersMatchingIp,
            timeoutInMs: 5000,
            cancellationToken);

        List<(GameServer server, int score)> scoredServers = respondingServers.Select(s =>
            {
                int score = 0;
                if (s.LastServerInfo is not null &&
                    s.LastServerInfo.HostName.Equals(connectedServerInfo.ServerName))
                {
                    score += 1;
                }

                if (s.LastStatusResponse is not null &&
                    s.LastStatusResponse.Players.Any(p => p.PlayerName.Equals(player.Name)))
                {
                    score += 2;
                }

                if (connectedServerInfo.PortGuess.HasValue &&
                    connectedServerInfo.PortGuess.Value == s.ServerPort)
                {
                    score += 2;
                }

                return (s, score);
            })
            .OrderByDescending(x => x.score)
            .ToList();

        _logger.LogDebug(
            "Found {numMatchingServers} matching servers for player {player} with connection info {connectionInfo}",
            scoredServers.Count,
            player,
            connectedServerInfo);

        (GameServer server, int score) matchingServer = scoredServers.FirstOrDefault(x => x.score > 0);
        if (matchingServer.server is null)
        {
            _logger.LogInformation("No matching server found for player {player} with connection info {connectionInfo}",
                player, connectedServerInfo);

        }

        _logger.LogInformation("Found matching server {server} for player {player} with confidence score of {matchConfidence}",
            matchingServer, player, matchingServer.score);

        return matchingServer;
    }

    private bool TryUpdateTrackedPlayerInfo(Player player, Func<Player, TrackedPlayerInfo, TrackedPlayerInfo> updateFunc)
    {
        // try to update a few times
        for (int i = 0; i < 100; i++)
        {
            if (!_trackedPlayers.TryGetValue(player, out TrackedPlayerInfo prevInfo))
            {
                return false;
            }

            TrackedPlayerInfo newInfo = updateFunc(player, prevInfo);

            if (_trackedPlayers.TryUpdate(player, newInfo, prevInfo))
            {
                return true;
            }
        }

        return false;
    }

    private bool StopTrackingPlayer(Player player)
    {
        if (_trackedPlayers.TryRemove(player, out _))
        {
            _logger.LogDebug("Stopped tracking player {player} due to exceeding max tracking time.", player);
            return true;
        }

        return false;
    }

    private async Task<int> RemoveServer(GameServer server, bool isTimeout)
    {
        if (_trackedGameServers.TryRemove(server))
        {
            bool[] results = await Task.WhenAll(server.KnownPlayers.Keys.Select(player =>
                RemovePlayerFromServer(player, server, isTimeout)
            ));

            int numPlayersRemoved = results.Count(success => success is true);

            _logger.LogInformation("Removed server {server} from tracking with {numPlayersRemoved}",
                server, numPlayersRemoved);
        }

        return 0;
    }

    #region Methods for Player - Server relationship

    private Task<bool> RemovePlayerFromCurrentServer(Player player, bool isTimeout = false, bool hasLock = false)
    {
        if (player.PlayingServer is null)
        {
            return Task.FromResult(false);
        }

        return RemovePlayerFromServer(player, player.PlayingServer, isTimeout, hasLock);
    }

    private async Task<bool> RemovePlayerFromServer(Player player, GameServer server, bool isTimeout = false, bool hasLock = false)
    {
        if (player.PlayingServer != server)
        {
            _logger.LogWarning("Cannot remove player {player} from server {server}: player is not on that server",
                player, server);
            return false;
        }

        DateTimeOffset startTime;

        using (await _playerLock.ConditionalLockAsync(player.Id, !hasLock))
        {
            if (player.PlayingServer != server)
            {
                _logger.LogWarning("Cannot remove player {player} from server {server}: player is not on that server",
                    player, server);
                return false;
            }

            if (!server.RemovePlayerInternal(player, out startTime))
            {
                return false;
            }

            player.PlayingServer = null;
        }

        _logger.LogDebug("Player {player} left server {server}", player, server);

        PlayerLeftServer?.Invoke(new PlayerLeftEventArgs()
        {
            Player = player,
            Server = server,
            JoinDate = startTime,
            LeaveDate = DateTimeOffset.Now,
        });

        return true;
    }

    private async Task<bool> AddPlayerToServer(Player player, GameServer server, bool migrate = true, bool hasLock = false)
    {
        if (player.PlayingServer is not null && !migrate)
        {
            return player.PlayingServer == server;
        }

        using (await _playerLock.ConditionalLockAsync(player.Id, !hasLock))
        {
            if (player.PlayingServer is not null)
            {
                if (player.PlayingServer == server)
                {
                    return true;
                }

                if (migrate)
                {
                    return await MigratePlayer(player, player.PlayingServer, server, verifySource: false, hasLock: true);
                }

                return false;
            }

            if (!server.AddPlayerInternal(player))
            {
                return false;
            }

            player.PlayingServer = null;
        }

        PlayerJoinedServer?.Invoke(player, server);

        return true;
    }

    /// <summary>
    /// Migrates a <paramref name="player"/> from the <paramref name="sourceServer"/> to the <paramref name="destinationServer"/>
    /// in a thread-safe operation.
    /// </summary>    
    /// <returns>Whether the player was successfully migrated</returns>
    private async Task<bool> MigratePlayer(
        Player player,
        GameServer sourceServer,
        GameServer destinationServer,
        bool verifySource = false,
        bool hasLock = false)
    {
        if (sourceServer.Id == destinationServer.Id)
        {
            // Already on the same server, nothing to do
            return true;
        }

        using var releaser = await _playerLock.ConditionalLockAsync(player.Id, !hasLock);

        // This is the critical section for atomicity.
        // We need to acquire locks on both servers to ensure that no other
        // operations (like adding/removing players) interfere during the transfer.
        // To prevent deadlocks, always acquire locks in a consistent order (e.g., by ID).
        GameServer lock1 = sourceServer.Id.CompareTo(destinationServer.Id) < 0 ? sourceServer : destinationServer;
        GameServer lock2 = sourceServer.Id.CompareTo(destinationServer.Id) < 0 ? destinationServer : sourceServer;

        lock (lock1.PlayerCollectionLock)
        {
            lock (lock2.PlayerCollectionLock)
            {
                // 1. Validate player is actually on the source server
                if (!sourceServer.ContainsPlayer(player) && player.PlayingServer is not null)
                {
                    // Player is not on the source server, cannot migrate from there.
                    // This could indicate a race condition if another thread already moved/removed the player.
                    return false;
                }

                // 2. Remove player from source server's collection
                if (!sourceServer.RemovePlayerInternal(player, out DateTimeOffset startTime) && verifySource)
                {
                    // Should not happen if ContainsPlayer was true, but good for robustness
                    return false;
                }

                // 3. Add player to destination server's collection
                if (!destinationServer.AddPlayerInternal(player))
                {
                    // This could happen if player was already somehow added to destination,
                    // or if there's a unique constraint issue.
                    // Depending on desired behavior, you might need to rollback source removal.
                    // For simplicity, we assume successful addition or a critical error.

                    _logger.LogWarning("Could not migrate player {player} from server {sourceServer} to {destinationServer}: already on destination, rolling back...",
                        player, sourceServer, destinationServer);

                    // Rollback
                    if (!sourceServer.AddPlayerInternal(player))
                    {
                        // well - f*$^
                        _logger.LogWarning("Failed to roll back migrating player {player} from server {sourceServer} to {destinationServer}",
                            player, sourceServer, destinationServer);
                    }
                    return false;
                }

                // 4. Update the player's server reference
                player.PlayingServer = destinationServer;

                _logger.LogInformation("Player {player} migrated from server {sourceServer} to {destinationServer}", player, sourceServer, destinationServer);

                PlayerLeftServer?.Invoke(new()
                {
                    Player = player,
                    Server = sourceServer,
                    JoinDate = startTime,
                    LeaveDate = DateTimeOffset.Now,
                });
                PlayerJoinedServer?.Invoke(player, destinationServer);
                return true;
            }
        }
    }

    #endregion
}
