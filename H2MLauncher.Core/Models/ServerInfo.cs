namespace H2MLauncher.Core.Models
{
    public record ServerInfo : IServerInfo
    {
        public required string Ip { get; init; }

        public required int Port { get; init; }

        public required string ServerName { get; init; }

        public int MaxClients { get; init; }

        public int Clients { get; init; }

        public int Bots { get; init; }

        public int RealPlayerCount { get; init; }

        public int PrivilegedSlots { get; init; }

        public bool HasMap { get; init; }

        public bool IsPrivate { get; init; }
    }
}
