using System.Runtime.CompilerServices;

using H2MLauncher.Core.Networking.GameServer;
using H2MLauncher.Core.Services;

namespace MatchmakingServer.SignalR;

/// <summary>
/// Common service to interact with game servers.
/// </summary>
public sealed class GameServerService
{
    private readonly ServerStore _serverStore;
    private readonly IGameServerCommunicationService<GameServer> _gameServerCommunicationService;

    public GameServerService(ServerStore serverStore, [FromKeyedServices("UDP")] IGameServerCommunicationService<GameServer> gameServerCommunicationService)
    {
        _serverStore = serverStore;
        _gameServerCommunicationService = gameServerCommunicationService;
    }


    /// <summary>
    /// Fetches the game server status for a given server object, either fresh or cached.
    /// </summary>
    /// <param name="gameServer">The game server.</param>
    /// <param name="maxAge">The maximum age of a cached response. If null, a fresh response is always fetched.</param>
    /// <returns>The status, or null if an error occurs.</returns>
    public Task<GameServerStatus?> GetServerStatusAsync(GameServer gameServer, TimeSpan? maxAge = null, CancellationToken cancellationToken = default)
    {
        // If maxAge is null, or the cached status is too old, fetch a fresh one
        if (maxAge == null ||
            gameServer.LastServerStatusTimestamp == null ||
            (DateTimeOffset.Now - gameServer.LastServerStatusTimestamp) > maxAge)
        {
            return FetchFreshStatusInternalAsync(gameServer, cancellationToken);
        }

        return Task.FromResult(gameServer.LastStatusResponse);
    }

    /// <summary>
    /// Fetches the game server info for a given server object, either fresh or cached.
    /// </summary>
    /// <param name="gameServer">The game server.</param>
    /// <param name="maxAge">The maximum age of a cached response. If null, a fresh response is always fetched.</param>
    /// <returns>The info, or null if an error occurs.</returns>
    public Task<GameServerInfo?> GetServerInfoAsync(GameServer gameServer, TimeSpan? maxAge = null, CancellationToken cancellationToken = default)
    {
        // If maxAge is null, or the cached info is too old, fetch a fresh one
        if (maxAge == null ||
            gameServer.LastServerInfoTimestamp == null ||
            (DateTimeOffset.Now - gameServer.LastServerStatusTimestamp) > maxAge)
        {
            return FetchFreshInfoInternalAsync(gameServer, cancellationToken);
        }

        return Task.FromResult(gameServer.LastServerInfo);
    }

    /// <summary>
    /// Fetches the game server status for multiple server objects, either fresh or cached.
    /// </summary>
    /// <param name="gameServers">The game servers.</param>
    /// <param name="maxAge">The maximum age of a cached response. If null, a fresh response is always fetched.</param>
    /// <returns>The processed servers.</returns>
    public async IAsyncEnumerable<GameServer> GetServerStatusAsync(
        IEnumerable<GameServer> gameServers,
        TimeSpan? maxAge = null,
        int timeoutInMs = 5000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ILookup<bool, GameServer> servers = gameServers.ToLookup(gameServer =>
        {
            return maxAge == null ||
                   gameServer.LastServerInfoTimestamp == null ||
                   (DateTimeOffset.Now - gameServer.LastServerStatusTimestamp) > maxAge;
        });


        IEnumerable<GameServer> serversWithCachedStatus = servers[false];
        IEnumerable<GameServer> serversToRefreshStatus = servers[true];

        // First yield cached
        foreach (GameServer gameServer in serversWithCachedStatus)
        {
            yield return gameServer;
        }

        if (!serversToRefreshStatus.Any())
        {
            // all cached, nothing to request
            yield break;
        }

        // Then request and yield status
        var responses = await _gameServerCommunicationService.GetStatusAsync(
            serversToRefreshStatus,
            requestTimeoutInMs: timeoutInMs,
            cancellationToken: cancellationToken);

        await foreach ((GameServer server, GameServerStatus? status) in responses.ConfigureAwait(false))
        {
            if (status is not null)
            {
                // only update if successful
                server.LastStatusResponse = status;
                server.LastServerStatusTimestamp = DateTimeOffset.Now;
            }

            yield return server;
        }
    }

    public async Task<List<GameServer>> RefreshInfoAndStatusAsync(
        IEnumerable<GameServer> gameServers,
        int timeoutInMs = 5000,
        CancellationToken cancellationToken = default)
    {
        List<GameServer> respondingServers = [];
        try
        {
            // Request server info for all servers matching ip
            Task getInfoCompleted = await _gameServerCommunicationService.SendGetInfoAsync(gameServers, (e) =>
            {
                e.Server.LastServerInfo = e.ServerInfo;
                e.Server.LastServerInfoTimestamp = DateTimeOffset.Now;

                respondingServers.Add(e.Server);
            }, timeoutInMs, cancellationToken: cancellationToken);

            // Immediately after send info requests send status requests
            Task getStatusCompleted = await _gameServerCommunicationService.SendGetStatusAsync(gameServers, (e) =>
            {
                e.Server.LastStatusResponse = e.ServerInfo;
                e.Server.LastServerStatusTimestamp = DateTimeOffset.Now;
            }, timeoutInMs, cancellationToken: cancellationToken);

            // Wait for all to complete / time out
            await Task.WhenAll(getInfoCompleted, getStatusCompleted);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // expected timeout
            return respondingServers;
        }

        return respondingServers;
    }

    private async Task<GameServerStatus?> FetchFreshStatusInternalAsync(GameServer gameServer, CancellationToken cancellationToken)
    {
        GameServerStatus? status = await _gameServerCommunicationService.GetStatusAsync(gameServer, cancellationToken);
        if (status != null)
        {
            gameServer.LastStatusResponse = status;
            gameServer.LastServerStatusTimestamp = DateTimeOffset.Now;
        }

        return status;
    }

    private async Task<GameServerInfo?> FetchFreshInfoInternalAsync(GameServer gameServer, CancellationToken cancellationToken)
    {
        GameServerInfo? info = await _gameServerCommunicationService.GetInfoAsync(gameServer, cancellationToken);
        if (info != null)
        {
            gameServer.LastServerInfo = info;
            gameServer.LastServerInfoTimestamp = DateTimeOffset.Now;
        }

        return info;
    }
}
