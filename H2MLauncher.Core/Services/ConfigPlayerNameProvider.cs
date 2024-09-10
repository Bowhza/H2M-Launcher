namespace H2MLauncher.Core.Services
{
    public sealed class ConfigPlayerNameProvider : IPlayerNameProvider
    {
        private readonly GameDirectoryService _gameDirectoryService;

        public string PlayerName { get; private set; }

        public event Action<string, string>? PlayerNameChanged;

        public ConfigPlayerNameProvider(GameDirectoryService gameDirectoryService)
        {
            _gameDirectoryService = gameDirectoryService;
            _gameDirectoryService.ConfigMpChanged += GameDirectoryService_ConfigMpChanged;


            PlayerName = _gameDirectoryService.CurrentConfigMp?.PlayerName ?? "Unknown Soldier";
        }

        private void GameDirectoryService_ConfigMpChanged(string path, GameDirectoryService.ConfigMpContent? newConfig)
        {
            if (newConfig is null || PlayerName.Equals(newConfig.PlayerName))
            {
                return;
            }

            string oldPlayerName = PlayerName;
            PlayerName = newConfig.PlayerName ?? "Unknown Soldier";
            PlayerNameChanged?.Invoke(oldPlayerName, PlayerName);
        }
    }
}
