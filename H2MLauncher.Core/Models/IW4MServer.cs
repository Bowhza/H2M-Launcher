using System.Net;
using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Models
{
    public class IW4MServer : IServerConnectionDetails
    {
        private string _hostName = "N/A";

        public required long Id { get; set; }
        public required string Version { get; set; }
        public required string Game { get; set; }
        public required string HostName
        {
            get => _hostName;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _hostName = WebUtility.HtmlDecode(value);
                }
            }
        }
        public required string Ip { get; set; }
        public required int Port { get; set; }
        public required string Map { get; set; }
        public required string GameType { get; set; }
        public required int ClientNum { get; set; }
        public required int MaxClientNum { get; set; }

        [JsonIgnore]
        public IW4MServerInstance Instance { get; set; } = null!;
    }
}
