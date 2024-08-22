using System.Text.Json;

namespace H2MLauncher.Core.Services
{
    public class H2MLauncherService(HttpClient httpClient, IErrorHandlingService errorHandlingService)
    {
        private const string GITHUB_REPOSITORY = "https://api.github.com/repos/Bowhza/H2M-Launcher/releases";
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        public const string CURRENT_VERSION = "H2M-v2.0.1";
        public string LatestKnownVersion { get; private set; } = "Unknown";


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
                LatestKnownVersion = doc.RootElement[0].GetProperty("tag_name").ToString();
                return LatestKnownVersion == CURRENT_VERSION;
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to check for updates; try again later.");
            }
            return false;
        }
    }
}
