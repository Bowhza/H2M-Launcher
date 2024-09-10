using System.Net;

namespace H2MLauncher.Core.Models
{
    public record GameServerStatus
    {
        public required IPEndPoint Address { get; init; }

        public List<(int Score, int Ping, string PlayerName)> Players { get; init; } = [];
    }
}
