using System.Net;

using H2MLauncher.Core.Services;

namespace H2MLauncher.Core.Models
{
    public class Server : IServerConnectionDetails
    {
        public required string Id { get; init; }

        public required string Ip { get; init; }

        public required int Port { get; init; }

        public IPEndPoint? Endpoint { get; init; }

        public required string Name { get; init; }

        public required int PlayerCount { get; init; }

        public required int MaxPlayerCount { get; init; }

        public int BotsCount { get; init; }
            
        public required string GameType { get; init; }

        public required string Map { get; init; }
            
        public required string Version { get; init; }
            
        public bool IsPrivate { get; init; }
        
        public required int Ping { get; init; }
    }
}
