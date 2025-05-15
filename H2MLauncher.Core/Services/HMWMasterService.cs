using System.Net.Http.Json;

using H2MLauncher.Core.Models;

using Microsoft.Extensions.Caching.Memory;

namespace H2MLauncher.Core.Services
{
    public class HMWMasterService(IErrorHandlingService errorHandlingService, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        : CachedMasterServerService(memoryCache, "HMW_SERVERS")
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public override async Task<IReadOnlySet<ServerConnectionDetails>> FetchServersAsync(CancellationToken cancellationToken)
        {
            HttpClient httpClient = _httpClientFactory.CreateClient(nameof(HMWMasterService));
            HashSet<ServerConnectionDetails> servers = [];
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync("game-servers", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return servers;
                }

                List<string>? addresses = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken);

                if (addresses is null)
                {
                    return servers;
                }
                
                foreach (string address in addresses)
                {
                    if (ServerConnectionDetails.TryParse(address, out var server))
                    {
                        servers.Add(server);
                    }
                }

                Cache.Set(CacheKey, servers, TimeSpan.FromMinutes(5));

                return servers;
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the HMW servers details at this time. Please try again later.");
                return servers;
            }
        }
    }
}
