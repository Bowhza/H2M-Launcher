namespace MatchmakingServer
{
    public record Player
    {
        public required string Name { get; init; }

        public required string ConnectionId { get; init; }

        public PlayerState State { get; set; }


        /// <summary>
        /// The server the player is queued or joined.
        /// </summary>
        public GameServer? Server { get; set; }
    }
}
