using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Models
{
    public class IW4MServerDetails
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        
        [JsonPropertyName("serverName")]
        public string ServerName { get; set; }
        
        [JsonPropertyName("listenAddress")]
        public string ListenAddress { get; set; }
        
        [JsonPropertyName("listenPort")]
        public int ListenPort { get; set; }

        [JsonPropertyName("game")]
        public string Game { get; set; }

        [JsonPropertyName("clientNum")]
        public int ClientNum { get; set; }

        [JsonPropertyName("maxClients")]
        public int MaxClients { get; set; }

        [JsonPropertyName("currentMap")]
        public Map Map { get; set; }

        [JsonPropertyName("currentGameType")]
        public GameType GameType { get; set; }

        [JsonPropertyName("parser")]
        public string Parser { get; set; }
    }
}

public class Map
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("alias")]
    public string Alias { get; set; }
}

public class GameType
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}
