using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Models
{
    public class IW4MServerStatus
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("isOnline")]
        public bool IsOnline { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("currentPlayers")]
        public int CurrentPlayers { get; set; }

        [JsonPropertyName("map")]
        public Map Map { get; set; }

        [JsonPropertyName("gameMode")]
        public string GameMode { get; set; }

        [JsonPropertyName("listenAddress")]
        public string ListenAddress { get; set; }

        [JsonPropertyName("listenPort")]
        public int ListenPort { get; set; }

        [JsonPropertyName("game")]
        public string Game { get; set; }

        [JsonPropertyName("players")]
        public List<Player> Players { get; set; }
    }

    public class Player
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("ping")]
        public int Ping { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("clientNumber")]
        public int ClientNumber { get; set; }

        [JsonPropertyName("connectionTime")]
        public int ConnectionTime { get; set; }

        [JsonPropertyName("level")]
        public string Level { get; set; }
    }
}
