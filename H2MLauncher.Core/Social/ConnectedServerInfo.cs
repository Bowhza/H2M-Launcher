namespace H2MLauncher.Core.Social
{
    /// <summary>
    /// Information about the game server the client is connected to.
    /// </summary>
    public record ConnectedServerInfo
    {
        public required string Ip { get; init; }

        public required string? ServerName { get; init; }

        public int? PortGuess { get; init; }
    }
}
