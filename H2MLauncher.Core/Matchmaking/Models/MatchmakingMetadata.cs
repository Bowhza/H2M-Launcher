namespace H2MLauncher.Core.Matchmaking.Models
{
    public enum MatchmakingQueueType
    {
        Solo,
        Party
    }

    public readonly record struct MatchmakingMetadata
    {
        /// <summary>
        /// Whether the player is actively searching
        /// </summary>
        public bool IsActiveSearcher { get; init; }

        /// <summary>
        /// Total number of players in the group
        /// </summary>
        public int TotalGroupSize { get; init; }

        /// <summary>
        /// When the group or player joined matchmaking
        /// </summary>
        public DateTime JoinTime { get; init; }

        /// <summary>
        /// What type of matchmaking ticket queued, e.g. solo or party.
        /// </summary>
        public MatchmakingQueueType QueueType { get; init; }

        /// <summary>
        /// The current match search preferences
        /// </summary>
        public MatchSearchCriteria? SearchPreferences { get; init; }

        /// <summary>
        /// The playlist associated with the match search
        /// </summary>
        public Playlist? Playlist { get; init; }
    }
}
