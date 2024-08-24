using System.Net.Http.Json;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class IW4MAdminService(ILogger<IW4MAdminService> logger, HttpClient httpClient) : IIW4MAdminService
    {
        private readonly ILogger<IW4MAdminService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        public async Task<IW4MServerDetails> GetServerDetailsAsync(string serverInstanceAddress, string id, CancellationToken cancellationToken)
        {
            // Validate parameters
            _logger.LogDebug("Validating parameters..");

            // Fetch server details from the Api
            string address = $"{serverInstanceAddress}/Api/Server/{id}";
            _logger.LogDebug("Fetching server details from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            _logger.LogInformation("Successfully fetched server details from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            IW4MServerDetails? serverDetails = await result.Content.ReadFromJsonAsync<IW4MServerDetails>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (serverDetails is null)
            {
                _logger.LogError("Failed to parse server details from response body: {response}", result.Content.ToString());
                // TODO: make proper exceptions
                throw new Exception();
            }
            _logger.LogInformation("Successfully parsed server details from json.");

            return serverDetails;
        }

        public async Task<IEnumerable<IW4MServerDetails>> GetServerListAsync(string serverInstanceAddress, CancellationToken cancellationToken)
        {
            // Validate parameters
            _logger.LogDebug("Validating parameters..");

            // Fetch server list from iw4m admin server instance
            string address = $"{serverInstanceAddress}/Api/Server";
            _logger.LogDebug("Fetching server list from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            _logger.LogInformation("Successfully fetched server list from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            IEnumerable<IW4MServerDetails>? servers = await result.Content.ReadFromJsonAsync<IEnumerable<IW4MServerDetails>>(cancellationToken);


            // Ensure the parsing success, otherwise provide an exception?
            if (servers is null)
            {
                _logger.LogError("Failed to parse server list from response body: {response}", result.Content.ToString());
                // TODO: make proper exceptions
                throw new Exception();
            }
            _logger.LogInformation("Successfully parsed server list from json.");

            return servers;
        }

        public async Task<IW4MServerStatus> GetServerStatusAsync(string serverInstanceAddress, string id, CancellationToken cancellationToken)
        {
            // Validate parameters
            _logger.LogDebug("Validating parameters..");

            // Fetch server status from the Api
            string address = $"{serverInstanceAddress}/Api/Status?id={id}";
            _logger.LogDebug("Fetching server status from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            _logger.LogInformation("Successfully fetched server status from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            List<IW4MServerStatus>? serverStatus = await result.Content.ReadFromJsonAsync<List<IW4MServerStatus>>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (serverStatus is null)
            {
                _logger.LogError("Failed to parse server status from response body: {response}", result.Content.ToString());
                // TODO: make proper exceptions
                throw new Exception();
            }

            // server not found
            if (serverStatus.Count != 1)
            {
                _logger.LogError("Server was not found: {}", result.Content.ToString());
                // TODO: make proper exceptions
                throw new Exception();
            }

            _logger.LogInformation("Successfully parsed server status from json.");

            return serverStatus.First();
        }
    }
}
