using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Models
{
    public class RaidMaxServerInstance
    {
        public List<RaidMaxServer> Servers { get; set; } = [];
        public string Id { get; set; }
        public string Version { get; set; }
        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; }
        [JsonPropertyName("webfront_url")]
        public string WebfrontUrl { get; set; }
        public long Uptime { get; set; }
        [JsonPropertyName("last_heartbeat")]
        public long LastHeartBeat { get; set; }
    }
}
