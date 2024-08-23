using System.Diagnostics;
using System.Text.Json;

using H2MLauncher.Core.Utilities;

namespace H2MLauncher.Core.Services
{
    public class H2MLauncherService
    {
        private const string GITHUB_REPOSITORY = "https://api.github.com/repos/Bowhza/H2M-Launcher/releases";
        public const string CURRENT_VERSION = "H2M-v2.0.3";
        private const string LAUNCHER = "H2MLauncher.UI.exe";
        private const string LAUNCHER_BACKUP = $"{LAUNCHER}.backup";
        private readonly HttpClient _httpClient;
        private readonly IErrorHandlingService _errorHandlingService;
        
        public string LatestKnownVersion { get; private set; } = "Unknown";

        public H2MLauncherService(HttpClient httpClient, IErrorHandlingService errorHandlingService)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "H2M-Launcher-App");
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        }

        public async Task<bool> IsLauncherUpToDateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // remove old version if it exists
                if (File.Exists(LAUNCHER_BACKUP))
                    File.Delete(LAUNCHER_BACKUP);
            }
            catch (Exception)
            {
                _errorHandlingService.HandleError("Couldn't delete old launcher.");
            }

            try
            {
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

        public async Task<bool> UpdateLauncherToLatestVersion(Action<double> progress, CancellationToken cancellationToken)
        {
            // download latest version
            string downloadUrl = $"https://github.com/Bowhza/H2M-Launcher/releases/download/{LatestKnownVersion}/H2MLauncher.UI.exe";
            string tempFileName2 = $"{LAUNCHER}.backup";
            string tempFileName = $"{LAUNCHER}.bak";

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "H2M-Launcher-App");
                DownloadProgressHandler downloadProgressHandler = (long? totalFileSize, long totalBytesDownloaded, double? progressPercentage) => 
                {
                    if (progressPercentage.HasValue)
                    {
                        progress(progressPercentage!.Value);
                        Debug.WriteLine($"{totalBytesDownloaded/1_000_000}/{totalFileSize/1_000_000}MB {progressPercentage}%");
                    }
                    else if (totalBytesDownloaded > 0)
                        Debug.WriteLine($"{totalBytesDownloaded / 1000000}MB");
                };

                await DownloadWithProgress.ExecuteAsync(_httpClient, downloadUrl, tempFileName, downloadProgressHandler);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to download the latest version. Please try again later.");
                return false;
            }

            try
            {
                // rename current exe to temp
                File.Move(LAUNCHER, tempFileName2);
                // rename new exe to current exe
                File.Move(tempFileName, LAUNCHER);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to modify files in directory. Please try again later.");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFileName))
                        File.Delete(tempFileName);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
            return true;
        }
    }
}
