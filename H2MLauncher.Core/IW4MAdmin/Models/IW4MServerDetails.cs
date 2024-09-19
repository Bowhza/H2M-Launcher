using System.Text.Json.Serialization;

namespace H2MLauncher.Core.IW4MAdmin.Models
{
    public class IW4MServerDetails
    {
        [JsonPropertyName("id")]
        public required long Id { get; set; }

        [JsonPropertyName("serverName")]
        public required string ServerName { get; set; }

        [JsonPropertyName("listenAddress")]
        public required string ListenAddress { get; set; }

        [JsonPropertyName("listenPort")]
        public required int ListenPort { get; set; }

        [JsonPropertyName("game")]
        public required string Game { get; set; }

        [JsonPropertyName("clientNum")]
        public required int ClientNum { get; set; }

        [JsonPropertyName("maxClients")]
        public required int MaxClients { get; set; }

        [JsonPropertyName("currentMap")]
        public required IW4MMap Map { get; set; }

        [JsonPropertyName("currentGameType")]
        public required IW4MGameType GameType { get; set; }

        [JsonPropertyName("parser")]
        public required string Parser { get; set; }
    }
}

public class IW4MMap
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("alias")]
    public required string Alias { get; set; }
}

public class IW4MGameType
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
