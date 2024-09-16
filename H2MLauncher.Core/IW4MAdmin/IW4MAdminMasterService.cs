using System.Net;

using Flurl;

using H2MLauncher.Core.IW4MAdmin.Models;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.IW4MAdmin
{
    public class IW4MAdminMasterService(ILogger<IW4MAdminMasterService> logger, HttpClient httpClient) : IIW4MAdminMasterService
    {
        private readonly ILogger<IW4MAdminMasterService> _logger = logger;
        private readonly HttpClient _httpClient = httpClient;

        public async Task<IReadOnlyList<IW4MServerInstance>> GetAllServerInstancesAsync(CancellationToken cancellationToken)
        {
            // Fetch server list from iw4m admin master server instance
            _logger.LogDebug("Fetching master server instance list from iw4m master server api..");

            HttpResponseMessage result = await _httpClient.GetAsync("instance", cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is not HttpStatusCode.OK)
            {
                return [];
            }

            _logger.LogInformation("Successfully fetched master server list from iw4m master server api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            List<IW4MServerInstance>? instances = await result.Content.TryReadFromJsonAsync<List<IW4MServerInstance>>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (instances is null)
            {
                _logger.LogWarning("Failed to parse master server list from response body: {response}", await result.Content.ReadAsStringAsync());
                return [];
            }
            _logger.LogInformation("Successfully parsed master server list from json.");

            foreach (var instance in instances)
            {
                instance.Servers.ForEach(s => s.Instance = instance);
            }

            return instances.AsReadOnly();
        }

        public async Task<IW4MServerInstance?> GetServerInstanceAsync(string id, CancellationToken cancellationToken)
        {
            // Fetch server instance from iw4m admin master server api
            string requestUrl = Url.Combine("instance", id);
            _logger.LogDebug("Fetching server instance {instanceId} from iw4m master server api..", id);
            HttpResponseMessage result = await _httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);

            if (result.StatusCode is not HttpStatusCode.OK)
            {
                return null;
            }

            _logger.LogInformation("Successfully fetched server instance from iw4m master server api.");

            // Parse it to Json
            _logger.LogDebug("Parsing response body to json..");
            IW4MServerInstance? instance = await result.Content.TryReadFromJsonAsync<IW4MServerInstance>(cancellationToken);

            // Ensure the parsing success, otherwise provide an exception?
            if (instance is null)
            {
                _logger.LogWarning("Failed to parse server instance from response body: {response}", result.Content.ToString());

                return null;
            }

            instance.Servers.ForEach(s => s.Instance = instance);

            _logger.LogInformation("Successfully parsed server instance from json.");

            return instance;
        }
    }
}
