using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Models
{
    public class IW4MServerInstance
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("version")]
        public required string Version { get; set; }

        [JsonPropertyName("ip_address")]
        public required string IpAddress { get; set; }

        [JsonPropertyName("webfront_url")]
        public required string WebfrontUrl { get; set; }

        [JsonIgnore]
        public string WebfrontUrlNormalized => WebfrontUrl.TrimEnd('/');

        [JsonPropertyName("uptime")]
        public required long Uptime { get; set; }

        [JsonPropertyName("last_heartbeat")]
        public required long LastHeartBeat { get; set; }

        [JsonPropertyName("servers")]
        public List<IW4MServer> Servers { get; set; } = [];
    }
}
