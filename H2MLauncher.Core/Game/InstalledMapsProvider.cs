using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;

using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Game
{
    public sealed class InstalledMapsProvider : IMapsProvider, IDisposable
    {
        private readonly GameDirectoryService _gameDirectoryService;
        private readonly IOptions<ResourceSettings> _resourceSettings;

        private readonly HashSet<string> _installedMaps;
        public IReadOnlySet<string> InstalledMaps { get; }

        public event Action<IMapsProvider>? MapsChanged;

        public InstalledMapsProvider(GameDirectoryService gameDirectoryService, IOptions<ResourceSettings> resourceSettings)
        {
            _gameDirectoryService = gameDirectoryService;

            _installedMaps = [];
            InstalledMaps = _installedMaps;

            _gameDirectoryService.UsermapsChanged += GameDirectoryService_UsermapsChanged;
            _gameDirectoryService.FastFileChanged += GameDirectoryService_FastFileChanged;
            _resourceSettings = resourceSettings;
        }

        private void GameDirectoryService_FastFileChanged(string fileName, string mapName)
        {
            UpdateInstalledMaps();
        }

        private void GameDirectoryService_UsermapsChanged(string? triggeredByPath, IReadOnlyList<string> usermaps)
        {
            UpdateInstalledMaps();
        }

        private void UpdateInstalledMaps()
        {
            _installedMaps.Clear();

            // in-game maps
            foreach (var mapName in _resourceSettings.Value.MapPacks.SelectMany(p => p.Maps.Select(m => m.Name)))
            {
                if (_gameDirectoryService.HasOgMap(mapName) != false)
                {
                    _installedMaps.Add(mapName);
                }
            }

            // usermaps
            foreach (var usermap in _gameDirectoryService.Usermaps)
            {
                _installedMaps.Add(usermap);
            }

            MapsChanged?.Invoke(this);
        }

        public void Dispose()
        {            
            _gameDirectoryService.UsermapsChanged -= GameDirectoryService_UsermapsChanged;
            _gameDirectoryService.FastFileChanged -= GameDirectoryService_FastFileChanged;
        }
    }
}
