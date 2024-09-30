using System.Diagnostics.CodeAnalysis;

namespace H2MLauncher.Core.Models
{
    public record JoinServerInfo : IFullServerConnectionDetails, ISimpleServerInfo
    {
        [SetsRequiredMembers]
        public JoinServerInfo(string ip, int port, string name)
        {
            Ip = ip;
            Port = port;
            ServerName = name;
        }

        public JoinServerInfo() { }

        public required string Ip { get; init; }
        public required int Port { get; init; }
        public required string ServerName { get; init; }
        public string? Password { get; init; }
    }
}
