using System.Reflection;
using System.Text.Json;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public sealed class H2MLauncherService
    {
        private const string GITHUB_REPOSITORY = "https://api.github.com/repos/tobibodamer/H2M-Launcher/releases";

        private readonly ILogger<H2MLauncherService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IErrorHandlingService _errorHandlingService;

        public static readonly string LauncherPath = Environment.ProcessPath ?? AssemblyName + ".exe";
        private static readonly string LauncherBackupPath = $"{LauncherPath}.backup";
        private static readonly string TempFileName = $"{LauncherPath}.bak";


        // IMPORTANT: Set this to the same pre-release label used in GitHub
        // (appended like '-beta') or empty when this is a normal release!
        public static readonly string CurrentPreReleaseLabel = "beta";
        public static string CurrentVersion
        {
            get
            {
                Version version = Assembly.GetEntryAssembly()!.GetName().Version!;
                string versionString = $"H2M-v{version.Major}.{version.Minor}.{version.Build}";
                if (!string.IsNullOrWhiteSpace(CurrentPreReleaseLabel))
                {
                    //H2M-v0.0.0-beta
                    versionString += "-" + CurrentPreReleaseLabel.ToLower();
                }

                if (version.Revision > 0)
                {
                    //H2M-v0.0.0-beta.1
                    versionString += "." + version.Revision;
                }

                return versionString;
            }
        }

        /// <summary>
        /// Gets the name of the entry assembly (without extension)
        /// </summary>
        private static string AssemblyName
        {
            get
            {
                return Assembly.GetEntryAssembly()!.GetName().Name ?? "H2MLauncher.UI";
            }
        }

        public string LatestKnownVersion { get; private set; } = "Unknown";

        public H2MLauncherService(ILogger<H2MLauncherService> logger, HttpClient httpClient, IErrorHandlingService errorHandlingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            // NOTE: GitHub requires a User-Agent to be set to interact with their API.
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "H2M-Launcher-App");
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        }

        public async Task<bool> IsLauncherUpToDateAsync(CancellationToken cancellationToken)
        {
            // Delete old launcher if present.
            try
            {
                // NOTE: When the launcher has previously been updated to the latest version,
                //       the old launcher named (H2MLauncher.UI.exe.backup) has to be deleted.
                //       It couldn't be deleted while that exe was still running; that is why
                //       on start up, we check if it is there to be deleted.
                if (File.Exists(LauncherBackupPath))
                {
                    _logger.LogDebug("Old launcher found; trying to delete it..");
                    File.Delete(LauncherBackupPath);
                    _logger.LogInformation("Old launcher deleted.");
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Couldn't delete old launcher.");
            }

            // Fetch the latest version tag to compare with the current version tag
            try
            {
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, GITHUB_REPOSITORY);
                httpRequestMessage.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                httpRequestMessage.Headers.Add("Accept", "application/vnd.github+json");

                HttpResponseMessage response = await _httpClient.SendAsync(httpRequestMessage, cancellationToken);
                response.EnsureSuccessStatusCode();
                JsonDocument doc = JsonDocument.Parse(response.Content.ReadAsStream(cancellationToken));
                if (doc.RootElement.GetArrayLength() == 0)
                {
                    return true;
                }
                LatestKnownVersion = doc.RootElement[0].GetProperty("tag_name").ToString();
                bool isUpToDate = LatestKnownVersion == CurrentVersion;

                if (!isUpToDate)
                    _logger.LogInformation("New launcher version available: {LatestKnownVersion}", LatestKnownVersion);

                return isUpToDate;
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to check for updates; try again later.");
            }

            _logger.LogInformation("Launcher is outdated: old {CurrentVersion}, new {LatestKnownVersion}.", CurrentVersion, LatestKnownVersion);
            return false;
        }

        public async Task<bool> UpdateLauncherToLatestVersion(Action<double> progress, CancellationToken cancellationToken)
        {
            string downloadUrl = $"https://github.com/Bowhza/H2M-Launcher/releases/download/{LatestKnownVersion}/{AssemblyName}.exe";

            try
            {
                DownloadProgressHandler downloadProgressHandler = (long? totalFileSize, long totalBytesDownloaded, double? progressPercentage) =>
                {
                    if (progressPercentage.HasValue)
                    {
                        progress(progressPercentage!.Value);
                        _logger.LogDebug("{totalBytesDownloaded}/{totalFileSize}MB {progressPercentage}%", totalBytesDownloaded / 1_000_000, totalFileSize / 1_000_000, progressPercentage);
                    }
                    else if (totalBytesDownloaded > 0)
                        _logger.LogDebug("{totalBytesDownloaded}MB", totalBytesDownloaded / 1_000_000);
                };

                // TODO: If user closed application while downloading, cancel the download -> cancellation token.
                //       Requires a bit more work to be done for global handling of cancellations.
                await DownloadWithProgress.ExecuteAsync(_httpClient, downloadUrl, TempFileName, downloadProgressHandler, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to download the latest version. Please try again later.");
                return false;
            }

            try
            {
                // NOTE: We move the current launcher as backup such that the new launcher can get the original 
                //       name for the exe. The backup launcher will be deleted on restart of the launcher.
                _logger.LogDebug("Attempting to move old launcher {} to {}.", LauncherPath, LauncherBackupPath);
                File.Move(LauncherPath, LauncherBackupPath);
                _logger.LogInformation("Moved old launcher {} to {}.", LauncherPath, LauncherBackupPath);

                _logger.LogDebug("Attempting to move new launcher {} to {}.", TempFileName, LauncherPath);
                File.Move(TempFileName, LauncherPath);
                _logger.LogInformation("Moved new launcher {} to {}.", TempFileName, LauncherPath);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to modify files in directory. Please try again later.");
                return false;
            }

            _logger.LogInformation("Launcher is updated to {LatestKnownVersion}.", LatestKnownVersion);

            return true;
        }
    }
}
