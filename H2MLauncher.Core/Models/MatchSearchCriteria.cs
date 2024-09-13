namespace H2MLauncher.Core.Models
{
    public record MatchSearchCriteria
    {
        /// <summary>
        /// Maximum ping of the match.
        /// </summary>
        public int MaxPing { get; init; } = -1;

        /// <summary>
        /// Maximum total score on the server.
        /// </summary>
        public int MaxScore { get; init; } = -1;

        /// <summary>
        /// Minimum players required to create a match (including the players already on the server).
        /// </summary>
        public int MinPlayers { get; init; } = 1;

        /// <summary>
        /// Maximum number of players on server without the match.
        /// </summary>
        public int MaxPlayersOnServer { get; init; } = -1;

        /// <summary>
        /// TODO: Max players in match + on server
        /// </summary>
        public int MaxTotalPlayers { get; init; } = -1;
    }

    public record MatchmakingPreferences
    {
        public required MatchSearchCriteria SearchCriteria { get; init; }

        public bool TryFreshGamesFirst { get; init; }
    }
}
