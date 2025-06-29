﻿using System.Net;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Networking.GameServer
{
    public record GameServerInfo
    {
        public required IPEndPoint Address { get; init; }

        public required string HostName { get; init; }

        public required string MapName { get; init; }

        public required string GameType { get; init; }

        public required string GameName { get; init; }

        public required string ModName { get; init; }

        public required string PlayMode { get; init; }

        public required int Clients { get; init; }

        public required int MaxClients { get; init; }

        public required int Bots { get; init; }

        public required int Ping { get; init; }

        public int Protocol { get; init; } = -1;

        public required bool IsPrivate { get; init; }

        public int PrivilegedSlots { get; init; } = -1;

        public int RealPlayerCount => Clients - Bots;

        public int FreeSlots => MaxClients - RealPlayerCount;

        public GamePlayerStatus[] Players { get; init; } = [];
    }
}
