using System.Net;
using System.Runtime.Serialization;

namespace H2MLauncher.Core.Models
{
    public class RaidMaxServer
    {
        private string hostName = "N/A";

        public double Id { get; set; }
        public string Version { get; set; }
        public string Game { get; set; }
        public string HostName
        {
            get => hostName;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    hostName = WebUtility.HtmlDecode(value);
                }
            }
        }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Map { get; set; }
        public string GameType { get; set; }
        public int ClientNum { get; set; }
        public int MaxClientNum { get; set; }
        
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
