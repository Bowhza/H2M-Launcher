using System.Net;

using Flurl;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class IW4MAdminService(ILogger<IW4MAdminService> logger, HttpClient httpClient) : IIW4MAdminService
    {
        private readonly ILogger<IW4MAdminService> _logger = logger;
        private readonly HttpClient _httpClient = httpClient;

        public async Task<IW4MServerDetails?> GetServerDetailsAsync(string serverInstanceAddress, string serverId, CancellationToken cancellationToken)
        {
            // Fetch server details from the Api
            string address = Url.Combine(serverInstanceAddress, "api", "server", serverId);
            _logger.LogDebug("Fetching server details from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is not HttpStatusCode.OK)
            {
                return null;
            }
            _logger.LogInformation("Successfully fetched server details from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            IW4MServerDetails? serverDetails = await result.Content.TryReadFromJsonAsync<IW4MServerDetails>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (serverDetails is null)
            {
                _logger.LogWarning("Failed to parse server details from response body: {response}", result.Content.ToString());

                return null;
            }
            _logger.LogInformation("Successfully parsed server details from json.");

            return serverDetails;
        }

        public async Task<IReadOnlyList<IW4MServerDetails>> GetServerListAsync(string serverInstanceAddress, CancellationToken cancellationToken)
        {
            // Validate parameters
            _logger.LogDebug("Validating parameters..");

            // Fetch server list from iw4m admin server instance
            string address = Url.Combine(serverInstanceAddress, "api", "server");
            _logger.LogDebug("Fetching server list from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is not HttpStatusCode.OK)
            {
                return [];
            }
            _logger.LogInformation("Successfully fetched server list from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            List<IW4MServerDetails>? servers = await result.Content.TryReadFromJsonAsync<List<IW4MServerDetails>>(cancellationToken);


            // Ensure the parsing success, otherwise provide an exception?
            if (servers is null)
            {
                _logger.LogWarning("Failed to parse server list from response body: {response}", result.Content.ToString());

                return [];
            }
            _logger.LogInformation("Successfully parsed server list from json.");

            return servers.AsReadOnly();
        }        

        public async Task<IW4MServerStatus?> GetServerStatusAsync(string serverInstanceAddress, string serverId, CancellationToken cancellationToken)
        {
            // Validate parameters
            _logger.LogDebug("Validating parameters..");

            // Fetch server status from the Api
            Url address = Url.Combine(serverInstanceAddress, "api", "status").SetQueryParam("id", serverId);
            _logger.LogDebug("Fetching server status from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is not HttpStatusCode.OK)
            {
                return null;
            }

            _logger.LogInformation("Successfully fetched server status from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            List<IW4MServerStatus>? serverStatus = await result.Content.TryReadFromJsonAsync<List<IW4MServerStatus>>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (serverStatus is null)
            {
                _logger.LogWarning("Failed to parse server status from response body: {response}", result.Content.ToString());

                return null;
            }

            // server not found
            if (serverStatus.Count != 1)
            {
                _logger.LogError("Server was not found: {}", result.Content.ToString());

                return null;
            }

            _logger.LogInformation("Successfully parsed server status from json.");

            return serverStatus.First();
        }

        public async Task<IReadOnlyList<IW4MServerStatus>> GetServerStatusListAsync(string serverInstanceAddress, CancellationToken cancellationToken)
        {
            // Validate parameters
            _logger.LogDebug("Validating parameters..");

            // Fetch server details from the Api
            string address = Url.Combine(serverInstanceAddress, "api", "status");
            _logger.LogDebug("Fetching server details from iw4m api..");
            HttpResponseMessage result = await _httpClient.GetAsync(address, cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is not HttpStatusCode.OK)
            {
                return [];
            }
            _logger.LogInformation("Successfully fetched server details from iw4m api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            List<IW4MServerStatus>? serverStatuses = await result.Content.TryReadFromJsonAsync<List<IW4MServerStatus>>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (serverStatuses is null)
            {
                _logger.LogWarning("Failed to parse server details from response body: {response}", result.Content.ToString());

                return [];
            }
            _logger.LogInformation("Successfully parsed server details from json.");

            return serverStatuses.AsReadOnly();
        }
    }
}
