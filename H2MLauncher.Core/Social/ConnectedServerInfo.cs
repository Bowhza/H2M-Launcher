namespace H2MLauncher.Core.Social
{
    public record ConnectedServerInfo
    {
        public required string Ip { get; init; }

        public required string? ServerName { get; init; }

        public int? PortGuess { get; init; }
    }
}
