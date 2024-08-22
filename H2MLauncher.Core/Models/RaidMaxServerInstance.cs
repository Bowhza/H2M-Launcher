using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Models
{
    public class RaidMaxServerInstance
    {
        public required string Id { get; set; }

        public required string Version { get; set; }

        [JsonPropertyName("ip_address")]
        public required string IpAddress { get; set; }

        [JsonPropertyName("webfront_url")]
        public required string WebfrontUrl { get; set; }

        public required long Uptime { get; set; }

        [JsonPropertyName("last_heartbeat")]
        public required long LastHeartBeat { get; set; }

        public List<RaidMaxServer> Servers { get; set; } = [];
    }
}
