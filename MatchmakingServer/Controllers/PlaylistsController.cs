using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using MatchmakingServer.Authentication;
using MatchmakingServer.Matchmaking;
using MatchmakingServer.Playlists;
using MatchmakingServer.Playlists.Dtos;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Nito.AsyncEx;

namespace MatchmakingServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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

        private readonly PlaylistStore _playlistStore;

        public PlaylistsController(
            IOptionsMonitor<ServerSettings> serverSettings,
            IWebHostEnvironment env,
            MatchmakingService matchmakingService,
            ServerStore serverStore,
            IMemoryCache memoryCache,
            [FromKeyedServices("UDP")] IGameServerInfoService<GameServer> udpGameServerCommunicationService,
            [FromKeyedServices("TCP")] IGameServerInfoService<GameServer> tcpGameServerCommunicationService,
            ILogger<PlaylistsController> logger,
            PlaylistStore playlistStore)
        {
            _serverSettings = serverSettings;
            _env = env;
            _matchmakingService = matchmakingService;
            _serverStore = serverStore;
            _memoryCache = memoryCache;
            _udpGameServerCommunicationService = udpGameServerCommunicationService;
            _tcpGameServerCommunicationService = tcpGameServerCommunicationService;
            _logger = logger;
            _playlistStore = playlistStore;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlaylistById(string id)
        {
            PlaylistDbo? playlist = await _playlistStore.GetPlaylist(id);
            if (playlist is null)
            {
                return NotFound();
            }

            if (playlist.Servers is not null)
            {
                int playerCount = await GetPlayerCountAsync(playlist);
                return Ok(playlist.ToPlaylistDto(playerCount));
            }

            return Ok(playlist.ToPlaylistDto());
        }

        [HttpGet("{id}/servers")]
        public async Task<IActionResult> GetPlaylistServersById(string id)
        {
            PlaylistDbo? playlist = await _playlistStore.GetPlaylist(id);
            if (playlist is null)
            {
                return NotFound();
            }

            if (playlist.Servers is null)
            {
                return NoContent();
            }

            List<GameServer> serversToRequest = [];
            foreach (ServerConnectionDetails connDetails in playlist.Servers)
            {
                GameServer server = _serverStore.GetOrAddServer(connDetails.Ip, connDetails.Port);

                if (server.LastServerInfo is null ||
                    DateTime.Now - server.LastSuccessfulPingTimestamp >= TimeSpan.FromMinutes(1))
                {
                    serversToRequest.Add(server);
                }
            }

            await RefreshServerInfo(serversToRequest, CancellationToken.None);

            return Ok(serversToRequest.Select(s =>
            {
                return new
                {
                    Address = $"{s.ServerIp}:{s.ServerPort}",
                    s.LastServerInfo?.HostName,
                    s.LastServerInfo?.Ping,
                    s.LastServerInfo?.RealPlayerCount,
                    s.LastServerInfo?.MaxClients,
                };
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPlaylists()
        {
            List<Playlist> result = [];
            List<Task> tasks = [];

            foreach (PlaylistDbo playlist in await _playlistStore.GetAllPlaylists())
            {
                tasks.Add(GetPlayerCountAsync(playlist).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        result.Add(playlist.ToPlaylistDto(playerCount: t.Result));
                    }
                    else
                    {
                        result.Add(playlist.ToPlaylistDto());
                    }
                }));
            }

            await tasks.WhenAll();

            _logger.LogTrace("Responding with {n} playlists", result.Count);

            return Ok(result);
        }

        private async Task<int> GetPlayerCountAsync(PlaylistDbo playlist)
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

                    foreach (ServerConnectionDetails connDetails in playlist.Servers)
                    {
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

        private async Task<List<GameServer>> RefreshServerInfo(IReadOnlyList<GameServer> servers, CancellationToken cancellationToken)
        {
            List<GameServer> respondingServers = new(servers.Count);
            _logger.LogTrace("Requesting server info for {numServers} servers...", servers.Count);
            try
            {
                // Request server info for all servers part of matchmaking rn
                Task getInfoCompleted = await _tcpGameServerCommunicationService.SendGetInfoAsync(servers, (e) =>
                {
                    e.Server.LastServerInfo = e.ServerInfo;
                    e.Server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;

                    respondingServers.Add(e.Server);
                }, timeoutInMs: 2000, cancellationToken: cancellationToken);

                // Wait for all to complete / time out
                await getInfoCompleted;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // expected timeout
                return respondingServers;
            }

            _logger.LogDebug("Server info received from {numServers}", respondingServers.Count);

            return respondingServers;
        }


        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpsertPlaylist(string id, CreatePlaylistDto playlist)
        {
            PlaylistDbo playlistToUpdate = new()
            {
                Id = id,
                Name = playlist.Name,
                Description = playlist.Description,
                Servers = playlist.Servers ?? []
            };

            bool successful = await _playlistStore.UpsertPlaylist(playlistToUpdate);
            if (successful)
            {
                int playerCount = await GetPlayerCountAsync(playlistToUpdate);

                return Ok(playlistToUpdate.ToPlaylistDto(playerCount));
            }

            return StatusCode(500);
        }

        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemovePlaylist(string id)
        {
            bool successful = await _playlistStore.RemovePlaylist(id);

            return successful ? NoContent() : NotFound();
        }
    }
}
