namespace H2MLauncher.Core.Settings
{
    public record ServerFilterSettings
    {
        public bool ShowEmpty { get; init; } = true;

        public bool ShowFull { get; init; } = true;

        public bool ShowPrivate { get; init; } = true;

        public int MaxPing { get; init; } = 999;

        public int MinPlayers { get; init; } = 1;

        public int MaxPlayers { get; init; } = 32;

        public int MaxSlots { get; init; } = 32;

        public List<string>? SelectedMapPacks { get; init; }

        public List<string>? SelectedGameModes { get; init; }

        public Dictionary<string, bool> ExcludeKeywords { get; init; } = [];
    }
}
