namespace H2MLauncher.Core.Models
{
    public record ServerInfo
    {
        public required string Ip { get; init; }

        public required int Port { get; init; }

        public required string ServerName { get; init; }

        public int MaxClients { get; init; }

        public int Clients { get; init; }

        public int Bots { get; init; }

        public int RealPlayerCount { get; init; }

        public int PrivilegedSlots { get; init; }

        public bool IsPrivate { get; init; }

        public required string GameName { get; init; }

        public required string MapName { get; init; }

        public required string GameType { get; init; }

        public required string PlayMode { get; init; }

        public required int Ping { get; init; }

        public int Protocol { get; init; }
    }
}
