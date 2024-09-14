namespace H2MLauncher.Core.Models
{
    public record SearchMatchResult
    {
        public required string ServerIp { get; init; }

        public required int ServerPort { get; init; }

        public int NumPlayers { get; init; }

        public int? ServerScore { get; init; }

        public double MatchQuality { get; init; }
    }
}
