using System.Text.Json.Serialization;

namespace H2MLauncher.Core.IW4MAdmin.Models
{
    public class IW4MServerStatus
    {
        [JsonPropertyName("id")]
        public required long Id { get; set; }

        [JsonPropertyName("isOnline")]
        public required bool IsOnline { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("maxPlayers")]
        public required int MaxPlayers { get; set; }

        [JsonPropertyName("currentPlayers")]
        public required int CurrentPlayers { get; set; }

        [JsonPropertyName("map")]
        public required IW4MMap Map { get; set; }

        [JsonPropertyName("gameMode")]
        public required string GameMode { get; set; }

        [JsonPropertyName("listenAddress")]
        public required string ListenAddress { get; set; }

        [JsonPropertyName("listenPort")]
        public required int ListenPort { get; set; }

        [JsonPropertyName("game")]
        public required string Game { get; set; }

        [JsonPropertyName("players")]
        public List<IW4MPlayer> Players { get; set; } = [];
    }
}
