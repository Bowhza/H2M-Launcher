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

        [JsonPropertyName("gamename")]
        public required string Game { get; init; }

        [JsonPropertyName("playmode")]
        public required string PlayMode { get; init; }

        [JsonPropertyName("sv_motd")]
        public string MOTD { get; init; } = "";

        [JsonPropertyName("clients")]
        public required int Clients { get; init; }

        [JsonPropertyName("sv_maxclients")]
        public required int MaxClients { get; init; }

        [JsonPropertyName("bots")]
        public required int Bots { get; init; }

        [JsonPropertyName("sv_privateClients")]
        public int PrivateClients { get; init; } = -1;

        [JsonPropertyName("dedicated")]
        public int Dedicated { get; init; }

        [JsonPropertyName("sv_running")]
        public int IsRunning { get; init; }

        [JsonPropertyName("isPrivate")]
        public int IsPrivate { get; init; }

        [JsonPropertyName("protocol")]
        public int Protocol { get; init; } = -1;
    }
}
