namespace H2MLauncher.Core.Models
{
    public record MatchmakingPreferences
    {
        public required MatchSearchCriteria SearchCriteria { get; init; }

        public bool TryFreshGamesFirst { get; init; }
    }
}
