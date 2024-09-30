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

        private readonly HashSet<ServerConnectionDetails> _servers = [];

        private const string CACHE_KEY = "HMW_SERVERS";

        public override async Task<IReadOnlySet<ServerConnectionDetails>> FetchServersAsync(CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            HttpClient httpClient = _httpClientFactory.CreateClient(nameof(HMWMasterService));
            try
            {
                response = await httpClient.GetAsync("game-servers", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return _servers;
                }

                List<string>? addresses = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken);
                
                if (addresses is null)
                {
                    return _servers;
                }

                _servers.Clear();
                foreach (string address in addresses)
                {
                    if (ServerConnectionDetails.TryParse(address, out var server))
                    {
                        _servers.Add(server);
                    }
                }

                Cache.Set(CACHE_KEY, _servers, TimeSpan.FromMinutes(5));

                return _servers;
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the HMW servers details at this time. Please try again later.");
                return _servers;
            }
        }
    }
}
