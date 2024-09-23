using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer.HMW;
using H2MLauncher.Core.Services;

using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Nito.AsyncEx;

namespace MatchmakingServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PlaylistsController : ControllerBase
    {
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private readonly ServerStore _serverStore;
        private readonly MatchmakingService _matchmakingService;
        private readonly IGameServerInfoService<GameServer> _udpGameServerCommunicationService;
        private readonly IGameServerInfoService<GameServer> _tcpGameServerCommunicationService;
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(
            IOptionsMonitor<ServerSettings> serverSettings,
            IWebHostEnvironment env,
            MatchmakingService matchmakingService,
            ServerStore serverStore,
            IMemoryCache memoryCache,
            [FromKeyedServices("UDP")] IGameServerInfoService<GameServer> udpGameServerCommunicationService,
            [FromKeyedServices("TCP")] IGameServerInfoService<GameServer> tcpGameServerCommunicationService,
            ILogger<PlaylistsController> logger)
        {
            _serverSettings = serverSettings;
            _env = env;
            _matchmakingService = matchmakingService;
            _serverStore = serverStore;
            _memoryCache = memoryCache;
            _udpGameServerCommunicationService = udpGameServerCommunicationService;
            _tcpGameServerCommunicationService = tcpGameServerCommunicationService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlaylistById(string id)
        {
            Playlist? playlist = _serverSettings.CurrentValue.Playlists.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (playlist is null)
            {
                return NotFound();
            }

            if (playlist.Servers is not null)
            {
                int playerCount = await GetPlayerCountAsync(playlist);
                return Ok(playlist with { CurrentPlayerCount = playerCount });
            }

            return Ok(playlist);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPlaylists()
        {
            List<Playlist> result = [];
            List<Task> tasks = [];

            foreach (Playlist playlist in _serverSettings.CurrentValue.Playlists)
            {
                tasks.Add(GetPlayerCountAsync(playlist).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        result.Add(playlist with { CurrentPlayerCount = t.Result });
                    }
                }));
            }

            await tasks.WhenAll();

            return Ok(result);
        }

        private async Task<int> GetPlayerCountAsync(Playlist playlist)
        {
            try
            {
                return await _memoryCache.GetOrCreateAsync("PLAYLIST_" + playlist.Id, async (entry) =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_serverSettings.CurrentValue.PlayerCountCacheExpirationInS);

                    if (playlist.Servers is null)
                    {
                        return 0;
                    }

                    int playerCount = 0;
                    List<GameServer> serverToRequest = [];

                    foreach (string address in playlist.Servers)
                    {
                        if (!ServerConnectionDetails.TryParse(address, out var connDetails))
                        {
                            continue;
                        }

                        GameServer server = _serverStore.GetOrAddServer(connDetails.Ip, connDetails.Port);

                        // players in queue
                        playerCount += server.PlayerQueue.Count;

                        if (server.LastServerInfo is not null &&
                            DateTime.Now - server.LastSuccessfulPingTimestamp < TimeSpan.FromMinutes(1))
                        {
                            // use player count of last server info
                            playerCount += server.LastServerInfo.RealPlayerCount;
                        }
                        else
                        {
                            serverToRequest.Add(server);
                        }

                        // players in matchmaking
                        playerCount += _matchmakingService.GetPlayersInServer(server).Count;
                    }

                    _logger.LogDebug("Requesting game server info for {numServers}", serverToRequest.Count);

                    if (playlist.Id.StartsWith("HMW", StringComparison.OrdinalIgnoreCase))
                    {
                        CancellationTokenSource timeoutCancellation = new(1500);
                        try
                        {
                            // request HMW servers with HTTP
                            await serverToRequest
                                .Select((s) => _tcpGameServerCommunicationService.GetInfoAsync(s, timeoutCancellation.Token).ContinueWith(t =>
                                {
                                    if (t.IsCompletedSuccessfully && t.Result is not null)
                                    {
                                        Interlocked.Add(ref playerCount, t.Result.Clients - t.Result.Bots);
                                    }
                                }))
                                .WhenAll();
                        }
                        catch (OperationCanceledException) { }
                        finally
                        {
                            timeoutCancellation.Dispose();
                        }
                    }
                    else
                    {
                        // request server info of all remaining servers
                        playerCount += await _udpGameServerCommunicationService
                             .GetAllInfoAsync(serverToRequest, requestTimeoutInMs: 1000)
                             .Select(r => r.info?.RealPlayerCount ?? 0)
                             .SumAsync();
                    }

                    return playerCount;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting player count for playlist {playlistId}", playlist.Id);
                return -1;
            }
        }
    }
}
