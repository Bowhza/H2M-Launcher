using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class RaidMaxService(HttpClient httpClient,
        IErrorHandlingService errorHandlingService, 
        ILogger<RaidMaxService> logger)
    {
        private const string APILINK = "http://master.iw4.zip/instance";
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        private readonly List<RaidMaxServer> _servers = [];
        private readonly ILogger<RaidMaxService> _logger = logger;

        public async Task<List<RaidMaxServer>> GetServerInfosAsync(CancellationToken cancellationToken)
        {
            List<RaidMaxServerInstance>? servers = [];
            _servers.Clear();

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(APILINK, cancellationToken);
                response.EnsureSuccessStatusCode();
                JsonSerializerOptions options = new JsonSerializerOptions();
                servers = await response.Content.ReadFromJsonAsync<List<RaidMaxServerInstance>>(cancellationToken);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the servers details at this time. Please try again later.");
            }

            if (servers is not null)
                _servers.AddRange(servers
                    .SelectMany(instance => instance.Servers)
                    .Where(server => server.Game == "H2M")
                    .ToList());

            return _servers;
        }
    }

    /// <summary>
    /// Class required for trimming file size so compiler knows what types are needed
    /// and prevents them from being removed.
    /// </summary>
    [JsonSerializable(typeof(List<string>))]
    public partial class JsonContext : JsonSerializerContext
    {
    }
}
