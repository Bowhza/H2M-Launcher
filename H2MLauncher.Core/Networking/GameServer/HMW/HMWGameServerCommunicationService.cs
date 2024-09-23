using System.Net.Http.Headers;
using System.Net.Http;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Networking.GameServer.HMW
{
    public sealed class HMWGameServerCommunicationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HMWGameServerCommunicationService> _logger;

        public HMWGameServerCommunicationService(ILogger<HMWGameServerCommunicationService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<ServerInfo?> GetInfoAsync(IServerConnectionDetails server, CancellationToken cancellationToken = default)
        {
            try
            {
                UriBuilder uriBuilder = new()
                {
                    Scheme = "http",
                    Host = server.Ip,
                    Port = server.Port,
                    Path = "getInfo"
                };

                Uri url = uriBuilder.Uri;

                HttpResponseMessage response;
                HttpEventListener.HttpRequestTimings timings;

                _httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true
                };

                using (HttpEventListener listener = new())
                {
                    response = await _httpClient.GetAsync(url, cancellationToken);
                    timings = listener.GetTimings();
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                HMWGameServerInfo? info = await response.Content.TryReadFromJsonAsync<HMWGameServerInfo>(cancellationToken);
                if (info is null)
                {
                    return null;
                }

                int ping = (int)(timings.Response?.TotalMilliseconds ?? timings.Request?.TotalMilliseconds ?? -1);

                return new ServerInfo()
                {
                    Ip = server.Ip,
                    Port = server.Port,
                    Clients = info.Clients,
                    Bots = info.Bots,
                    MaxClients = info.MaxClients,
                    IsPrivate = info.IsPrivate == 1,
                    PrivilegedSlots = info.PrivateClients,
                    RealPlayerCount = info.Clients - info.Bots,
                    ServerName = info.HostName,
                    GameName = info.Game,
                    GameType = info.GameType,
                    MapName = info.MapName,
                    PlayMode = info.PlayMode,
                    Protocol = info.Protocol,
                    Ping = ping
                };
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while requesting server info from {server}", server);

                return null;
            }
        }
    }
}
