using System.Net;
using System.Runtime.Serialization;

namespace H2MLauncher.Core.Models
{
    public class RaidMaxServer
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
        
        [IgnoreDataMember]
        public long Ping { get; set; }
        [IgnoreDataMember]
        public string Occupation => $"{ClientNum}/{MaxClientNum}";
        [IgnoreDataMember]
        public string PingDisplay => Ping == -1 ? "N/A" : Ping.ToString();

        public override string ToString()
        {
            return $"connect {Ip}:{Port}";
        }
    }
}
