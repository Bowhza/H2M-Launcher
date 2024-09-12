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
        /// Minimum players required to create a match.
        /// </summary>
        public int MinPlayers { get; init; } = 1;

        public int MaxPlayers { get; init; } = -1;
    }
}
