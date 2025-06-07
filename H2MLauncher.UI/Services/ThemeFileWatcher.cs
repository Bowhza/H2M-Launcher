using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive.Linq;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.UI.Services
{
    internal sealed class ThemeFileWatcher
    {
        private readonly CustomizationManager _customizationManager;
        private readonly ILogger<ThemeFileWatcher> _logger;

        private FileSystemWatcher? _fileSystemWatcher;
        private IDisposable? _eventSubscription;

        public ThemeFileWatcher(IOptionsMonitor<H2MLauncherSettings> optionsMonitor, ILogger<ThemeFileWatcher> logger, CustomizationManager customizationManager)
        {
            _logger = logger;
            _customizationManager = customizationManager;

            string? currentTheme = optionsMonitor.CurrentValue.Customization?.Themes?.FirstOrDefault();
            optionsMonitor.OnChange((settings, _) =>
            {
                if (settings.Customization?.HotReloadThemes == true)
                {
                    string? newTheme = settings.Customization?.Themes?.FirstOrDefault();
                    if (currentTheme == newTheme && _fileSystemWatcher is not null)
                    {
                        return;
                    }

                    currentTheme = newTheme;
                    WatchThemes(currentTheme);
                }
                else
                {
                    UninitializeWatcher();
                }
            });

            if (currentTheme is not null && 
                optionsMonitor.CurrentValue.Customization?.HotReloadThemes == true)
            {
                WatchThemes(currentTheme);
            }
        }

        private void WatchThemes(string? theme)
        {
            if (theme is null)
            {
                UninitializeWatcher();
                return;
            }

            if (_fileSystemWatcher is null)
            {
                InitializeWatcher(theme);
            }
            else
            {
                _fileSystemWatcher.Filters.Clear();
                _fileSystemWatcher.Filters.Add(Path.GetFileName(theme));
                _fileSystemWatcher.Path = Path.GetDirectoryName(theme)!;
            }
        }


        [MemberNotNull(nameof(_fileSystemWatcher))]
        private void InitializeWatcher(string path)
        {
            _logger.LogDebug("Start watching game directory {gameDir}", path);

            _fileSystemWatcher = new FileSystemWatcher()
            {
                Path = Path.GetDirectoryName(path)!,
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            _fileSystemWatcher.Filters.Add(Path.GetFileName(path));

            _fileSystemWatcher.Error += FileSystemWatcher_Error;

            _eventSubscription = Observable
                .FromEventPattern<FileSystemEventArgs>(_fileSystemWatcher, nameof(_fileSystemWatcher.Changed))
                .Select(e => e.EventArgs)
                .Throttle(TimeSpan.FromSeconds(0.5))
                .Subscribe(OnThemeFileChanged);
        }

        private void UninitializeWatcher()
        {
            if (_fileSystemWatcher is not null)
            {
                _fileSystemWatcher.Dispose();
                _eventSubscription?.Dispose();
                _fileSystemWatcher.Error -= FileSystemWatcher_Error;
                _fileSystemWatcher = null;
            }
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "Error of theme file system watcher:");
        }

        private void OnThemeFileChanged(FileSystemEventArgs e)
        {
            try
            {
                _logger.LogTrace("Theme file changed: {path}, {changeType}", e.FullPath, e.ChangeType);

                _customizationManager.LoadTheme(e.FullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling theme file watcher event");
            }
        }
    }
}
