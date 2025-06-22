using static H2MLauncher.Core.Services.GameDirectoryService;

namespace H2MLauncher.Core.Game
{
    public interface IGameConfigProvider
    {
        public ConfigMpContent? CurrentConfigMp { get; }

        public event ConfigChangedEventHandler? ConfigMpChanged;
    }
}