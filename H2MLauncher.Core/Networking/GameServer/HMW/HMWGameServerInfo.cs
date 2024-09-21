using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Networking.GameServer.HMW
{
    public record HMWGameServerInfo
    {
        [JsonPropertyName("hostname")]
        public required string HostName { get; init; }

        [JsonPropertyName("mapname")]
        public required string MapName { get; init; }

        [JsonPropertyName("gametype")]
        public required string GameType { get; init; }

        [JsonPropertyName("playmode")]
        public required string PlayMode { get; init; }

        [JsonPropertyName("clients")]
        public required int Clients { get; init; }

        [JsonPropertyName("sv_maxclients")]
        public required int MaxClients { get; init; }

        [JsonPropertyName("bots")]
        public required int Bots { get; init; }

        [JsonPropertyName("sv_privateClients")]
        public required int PrivateClients { get; init; }
    }
}
