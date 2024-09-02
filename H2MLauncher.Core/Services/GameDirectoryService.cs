using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Services
{
    public sealed partial class GameDirectoryService : IDisposable
    {
        public record ConfigMpContent(string? PlayerName, string? LastHostName, int MaxFps);

        private const string CONFIG_MP_FILENAME = "config_mp.cfg";
        private const string PLAYERS2_DIR = "players2";
        private const string USERMAPS_DIR = "h2m-usermaps";

        private FileSystemWatcher? _fileSystemWatcher;
        private readonly IOptionsMonitor<H2MLauncherSettings> _optionsMonitor;
        private readonly ILogger<GameDirectoryService> _logger;
        private readonly IDisposable? _optionsMonitorChangeRegistration;
        private readonly List<string> _usermaps = [];

        public string? CurrentDir { get; private set; }
        public bool IsWatching => _fileSystemWatcher != null;

        public IReadOnlyList<string> Usermaps { get; }

        public ConfigMpContent? CurrentConfigMp { get; private set; }

        public event Action<string, ConfigMpContent?>? ConfigMpChanged;


        public GameDirectoryService(IOptionsMonitor<H2MLauncherSettings> optionsMonitor, ILogger<GameDirectoryService> logger)
        {
            Usermaps = _usermaps.AsReadOnly();
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _optionsMonitorChangeRegistration = optionsMonitor.OnChange((settings, _) =>
            {
                if (settings.WatchGameDirectory)
                {
                    WatchGameDirectory(settings);
                }
                else if (IsWatching)
                {
                    UninitializeWatcher();
                }
            });

            if (_optionsMonitor.CurrentValue.WatchGameDirectory)
            {
                WatchGameDirectory(optionsMonitor.CurrentValue);
            }
        }

        private void WatchGameDirectory(H2MLauncherSettings settings)
        {
            CurrentDir = GetGameDir(settings);
            if (CurrentDir is null)
            {
                UninitializeWatcher();
                return;
            }

            if (_fileSystemWatcher is null)
            {
                InitializeWatcher(CurrentDir);
            }
            else
            {
                _fileSystemWatcher.Path = CurrentDir;
            }

            OnConfigFileChanged(Path.Combine(CurrentDir, PLAYERS2_DIR, CONFIG_MP_FILENAME));
            OnUsermapsChanged(Path.Combine(CurrentDir, USERMAPS_DIR));
        }

        private static string? GetGameDir(H2MLauncherSettings settings)
        {
            string? gameDir = Path.GetDirectoryName(settings.MWRLocation);
            if (gameDir is null)
            {
                return null;
            }

            if (!Directory.Exists(gameDir))
            {
                return null;
            }

            return gameDir;
        }

        [MemberNotNull(nameof(_fileSystemWatcher))]
        private void InitializeWatcher(string path)
        {
            _logger.LogDebug("Start watching game directory {gameDir}", path);

            _fileSystemWatcher = new FileSystemWatcher(path)
            {
                Filter = CONFIG_MP_FILENAME,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };
            _fileSystemWatcher.Changed += FileSystemWatcherEvent;
            _fileSystemWatcher.Created += FileSystemWatcherEvent;
            _fileSystemWatcher.Deleted += FileSystemWatcherEvent;
            _fileSystemWatcher.Error += FileSystemWatcher_Error;
        }

        private void UninitializeWatcher()
        {
            if (_fileSystemWatcher is not null)
            {
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher.Changed -= FileSystemWatcherEvent;
            }
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "Error of game directory file system watcher:");

            // reinitialize
            WatchGameDirectory(_optionsMonitor.CurrentValue);
        }

        private void FileSystemWatcherEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogTrace("Game directory file changed: {path}, {changeType}", e.FullPath, e.ChangeType);

                string currentDirAbsolutePath = Path.GetFullPath(CurrentDir ?? "");

                if (e.FullPath.Equals(Path.Combine(currentDirAbsolutePath, PLAYERS2_DIR, CONFIG_MP_FILENAME)))
                {
                    OnConfigFileChanged(e.FullPath);
                }
                else if (e.FullPath.StartsWith(Path.Combine(currentDirAbsolutePath, USERMAPS_DIR)))
                {
                    OnUsermapsChanged(Path.Combine(currentDirAbsolutePath, USERMAPS_DIR));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling game directory watcher event");
            }
        }

        private void OnConfigFileChanged(string path)
        {
            if (!File.Exists(path))
            {
                CurrentConfigMp = null;
                ConfigMpChanged?.Invoke(path, null);
                return;
            }

            _logger.LogTrace("Config file change detected, parsing...");

            string content = File.ReadAllText(path);

            MatchCollection matches = ConfigEntriesRegex().Matches(content);

            string? playerName = null;
            string? sv_hostName = null;
            int com_maxFps = -1;

            foreach (Match match in matches.Where(m => m.Success))
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                uint hash;
                try
                {
                    // parse hex code
                    hash = Convert.ToUInt32(key, 16);
                }
                catch
                {
                    continue;
                }

                switch (hash)
                {
                    case MwrDvarHashes.NAME:
                        playerName = value;
                        break;
                    case MwrDvarHashes.SV_HOSTNAME:
                        sv_hostName = value;
                        break;
                    case MwrDvarHashes.COM_MAXFPS:
                        _ = int.TryParse(value, out com_maxFps);
                        break;
                }
            }

            _logger.LogInformation("Parsed '{configFile}': {config}", CONFIG_MP_FILENAME, CurrentConfigMp);

            CurrentConfigMp = new(playerName, sv_hostName, com_maxFps);
            ConfigMpChanged?.Invoke(path, CurrentConfigMp);
        }

        private void OnUsermapsChanged(string usermapsDir)
        {
            _usermaps.Clear();
            foreach (var dir in Directory.EnumerateDirectories(usermapsDir))
            {
                string folderName = Path.GetFileName(dir);
                string usermapFile = Path.Combine(dir, $"{folderName}.ff");
                string usermapLoadFile = Path.Combine(dir, $"{folderName}_load.ff");
                string usermapPakFile = Path.Combine(dir, $"{folderName}.pak");

                if (File.Exists(usermapFile))
                {
                    _usermaps.Add(folderName);
                }
            }
        }

        public void Dispose()
        {
            UninitializeWatcher();
            _optionsMonitorChangeRegistration?.Dispose();
        }

        [GeneratedRegex(@"seta\s+(0x[0-9A-F]+)\s+""(.*?)""")]
        private static partial Regex ConfigEntriesRegex();
    }
}
