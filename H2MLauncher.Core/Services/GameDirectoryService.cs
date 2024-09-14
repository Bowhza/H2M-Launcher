using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
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

        public event Action<string?, IReadOnlyList<string>>? UsermapsChanged;

        public event Action<string, string>? FastFileChanged;


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
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };
            _fileSystemWatcher.Filters.Add("*.ff");
            _fileSystemWatcher.Filters.Add(CONFIG_MP_FILENAME);

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
                    OnUsermapsChanged(Path.Combine(currentDirAbsolutePath, USERMAPS_DIR), e.FullPath);
                }
                else
                {
                    string relativePath = e.FullPath[currentDirAbsolutePath.Length..];
                    if (relativePath.EndsWith(".ff"))
                    {
                        OnFastFileChanged(Path.GetFileName(relativePath), e.FullPath);
                    }
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

            string content;

            // open file with read write share
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.Default))
            {
                content = sr.ReadToEnd();
            }

            if (content == "")
            {
                // probably empty content because file cleared by game for a moment
                return;
            }

            // parse hashed config entries
            // TODO: this could be done with a dict in the future parsing all dvars
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

            _logger.LogTrace("Parsed '{configFile}': {config}", CONFIG_MP_FILENAME, CurrentConfigMp);

            ConfigMpContent newContent = new(playerName, sv_hostName, com_maxFps);
            if (!newContent.Equals(CurrentConfigMp))
            {
                _logger.LogInformation("New '{configFile}' loaded: {config}", CONFIG_MP_FILENAME, newContent);
                CurrentConfigMp = newContent;
                ConfigMpChanged?.Invoke(path, CurrentConfigMp);
            }
        }

        private void OnUsermapsChanged(string usermapsDir, string? triggeredByPath = null)
        {
            if (!Directory.Exists(usermapsDir))
            {
                _logger.LogTrace("Usermaps directory not found");

                _usermaps.Clear();
                UsermapsChanged?.Invoke(usermapsDir, Usermaps);
                return;
            }

            _logger.LogTrace("Usermaps changed, updating...");

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

            _logger.LogInformation("Usermaps changed, detected {numUsermaps} maps", _usermaps.Count);

            UsermapsChanged?.Invoke(triggeredByPath, Usermaps);
        }

        private void OnFastFileChanged(string fileName, string fullPath)
        {
            _logger.LogInformation("Detected fast file change: {fastfileName}", fileName);

            FastFileChanged?.Invoke(fileName, fullPath);
        }

        public bool? HasOgMap(string mapName)
        {
            string? gameDir = GetGameDir(_optionsMonitor.CurrentValue);
            if (string.IsNullOrEmpty(gameDir))
            {
                return null;
            }

            // look for fastfile
            string ff = $"{mapName}.ff";

            // check in game directory
            string mapFile = Path.Combine(gameDir, ff);
            if (File.Exists(mapFile))
            {
                return true;
            }

            // check in 'zone' subfolder
            mapFile = Path.Combine(gameDir, "zone", ff);
            return File.Exists(mapFile);
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
