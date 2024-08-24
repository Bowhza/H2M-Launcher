using System.Net.Http.Json;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class IW4MAdminMasterService : IIW4MAdminMasterService
    {
        private readonly ILogger<IW4MAdminMasterService> _logger;
        private readonly HttpClient _httpClient;
        // TODO: create configuration/settings file
        private readonly string _masterServiceUrl = "http://master.iw4.zip/";

        public IW4MAdminMasterService(ILogger<IW4MAdminMasterService> logger, HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<IEnumerable<IW4MAdminServerInstance>> GetAllServerInstancesAsync(CancellationToken cancellationToken)
        {
            // Fetch server list from iw4m admin master server instance
            string address = $"{_masterServiceUrl}/instance";
            _logger.LogDebug("Fetching master server list from iw4m master server api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            _logger.LogInformation("Successfully fetched master server list from iw4m master server api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            IEnumerable<IW4MAdminServerInstance>? servers = await result.Content.ReadFromJsonAsync<IEnumerable<IW4MAdminServerInstance>>(cancellationToken);


            // Ensure the parsing success, otherwise provide an exception?
            if (servers is null)
            {
                _logger.LogError("Failed to parse master server list from response body: {response}", result.Content.ToString());
                // TODO: make proper exceptions
                throw new Exception();
            }
            _logger.LogInformation("Successfully parsed master server list from json.");

            return servers;
        }

        public async Task<IW4MAdminServerInstance> GetServiceInstanceAsync(string id, CancellationToken cancellationToken)
        {
            // Fetch server instance from iw4m admin master server api
            string address = $"{_masterServiceUrl}/instance/{id}";
            _logger.LogDebug("Fetching server instance from iw4m master server api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            _logger.LogInformation("Successfully fetched server instance from iw4m master server api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            IW4MAdminServerInstance? servers = await result.Content.ReadFromJsonAsync<IW4MAdminServerInstance>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (servers is null)
            {
                _logger.LogError("Failed to parse server instance from response body: {response}", result.Content.ToString());
                // TODO: make proper exceptions
                throw new Exception();
            }
            _logger.LogInformation("Successfully parsed server instance from json.");

            return servers;
        }
    }
}
