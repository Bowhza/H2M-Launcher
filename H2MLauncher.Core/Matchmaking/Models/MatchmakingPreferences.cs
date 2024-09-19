namespace H2MLauncher.Core.Matchmaking.Models
{
    public record MatchmakingPreferences
    {
        public required MatchSearchCriteria SearchCriteria { get; init; }

        public bool TryFreshGamesFirst { get; init; }
    }
}
