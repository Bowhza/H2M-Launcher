using System.Text.Json.Serialization;

using Flurl;

namespace H2MLauncher.Core.Settings
{
    public sealed record MatchmakingSettings
    {
        public string MatchmakingServerUrl { get; init; } = "";

        [JsonIgnore]
        public string QueueingHubUrl => Url.Combine(MatchmakingServerUrl, "Queue");

        [JsonIgnore]
        public string ServerDataUrl => Url.Combine(MatchmakingServerUrl, "servers/data");
    }
}
