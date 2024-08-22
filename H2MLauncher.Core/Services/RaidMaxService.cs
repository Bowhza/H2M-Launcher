using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public class RaidMaxService(HttpClient httpClient, IErrorHandlingService errorHandlingService)
    {
        private const string APILINK = "http://master.iw4.zip/instance";
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        private readonly List<RaidMaxServer> _servers = [];

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

        public bool SaveServerList()
        {
            // Create a list of "Ip:Port" strings
            List<string> ipPortList = _servers.ConvertAll(server => $"{server.Ip}:{server.Port}");

            // Serialize the list into JSON format
            string jsonString = JsonSerializer.Serialize(ipPortList, JsonContext.Default.ListString);

            try
            {
                // Store the server list into the corresponding directory
                Trace.WriteLine("Storing server list into \"/players2/favourites.json\"");
                File.WriteAllText("./players2/favourites.json", jsonString);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Could not save favourites.json file. Make sure the exe is inside the root of the game folder.");
                return false;
            }
            return true;
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
