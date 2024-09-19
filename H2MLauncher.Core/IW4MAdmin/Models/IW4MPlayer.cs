using System.Text.Json.Serialization;

namespace H2MLauncher.Core.IW4MAdmin.Models
{
    public class IW4MPlayer
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("score")]
        public required int Score { get; set; }

        [JsonPropertyName("ping")]
        public required int Ping { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("state")]
        public required IW4MClientState State { get; set; }

        [JsonPropertyName("clientNumber")]
        public required int ClientNumber { get; set; }

        [JsonPropertyName("connectionTime")]
        public required int ConnectionTime { get; set; }

        [JsonPropertyName("level")]
        public required string Level { get; set; }
    }
}
