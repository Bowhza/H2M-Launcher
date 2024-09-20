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

        public async Task<HMWGameServerInfo?> GetInfoAsync(IServerConnectionDetails server, CancellationToken cancellationToken = default)
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

                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.TryReadFromJsonAsync<HMWGameServerInfo>(cancellationToken).ConfigureAwait(false);
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
