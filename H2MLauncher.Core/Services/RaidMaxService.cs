using System.Net.Http.Json;
using System.Text.Json.Serialization;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Services
{
    public class RaidMaxService(HttpClient httpClient,
        IErrorHandlingService errorHandlingService, 
        ILogger<RaidMaxService> logger,
        IOptionsMonitor<H2MLauncherSettings> options)
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        private readonly List<RaidMaxServer> _servers = [];
        private readonly ILogger<RaidMaxService> _logger = logger;
        private readonly IOptionsMonitor<H2MLauncherSettings> _options = options;

        public async Task<List<RaidMaxServer>> GetServerInfosAsync(CancellationToken cancellationToken)
        {
            List<RaidMaxServerInstance>? servers = [];
            _servers.Clear();

            try
            {
                string url = $"{_options.CurrentValue.IW4MMasterServerUrl.TrimEnd('/')}/instance";
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? baseUrl))
                {
                    _errorHandlingService.HandleError("Invalid master server url in settings.");
                    return _servers;
                }

                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();                
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
