using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace H2MLauncher.Core.Models
{
    public record GameServerInfo
    {
        public required IPEndPoint Address { get; init; }

        public required string HostName { get; init; }

        public required string MapName { get; init; }

        public required string GameType { get; init; }

        public required string ModName { get; init; }

        public required string PlayMode { get; init; }

        public required int Clients { get; init; }

        public required int MaxClients { get; init; }

        public required int Bots { get; init; }

        public required int Ping { get; init; }

        public required bool IsPrivate { get; init; }
    }
}
