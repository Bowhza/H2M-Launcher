using System.Net.Http.Json;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public class HMWMasterService(IErrorHandlingService errorHandlingService, IHttpClientFactory httpClientFactory) : IMasterServerService
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        private readonly List<ServerConnectionDetails> _servers = [];

        public async Task<IReadOnlyList<ServerConnectionDetails>> FetchServersAsync(CancellationToken cancellationToken)
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
