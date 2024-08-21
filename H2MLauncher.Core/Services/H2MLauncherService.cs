using System.Diagnostics;
using System.Text.Json;

namespace H2MLauncher.Core.Services
{
    public class H2MLauncherService(HttpClient httpClient)
    {
        private const string GITHUB_REPOSITORY = "https://api.github.com/repos/Bowhza/H2M-Launcher/releases";
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private const string CURRENT_VERSION = "H2M-v1.1.0";

        public async Task<bool> IsLauncherUpToDateAsync(CancellationToken cancellationToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "H2M-Launcher-App");
                _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                HttpResponseMessage response = await _httpClient.GetAsync(GITHUB_REPOSITORY, cancellationToken);
                response.EnsureSuccessStatusCode();
                JsonDocument doc = JsonDocument.Parse(response.Content.ReadAsStream(cancellationToken));
                string latestVersion = doc.RootElement[0].GetProperty("tag_name").ToString();
                return latestVersion == CURRENT_VERSION;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                Console.WriteLine("Unable to check for updates; try again later.");
            }
            return false;
        }
    }
}
