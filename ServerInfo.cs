using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace H2M_Launcher
{
    public class ServerInfo
    {
        public string? Hostname { get; set; }
        public string? Map { get; set; }
        public string? ClientNum { get; set; }
        public string? MaxClientNum { get; set; }
        public string? GameType { get; set; }
        public string? Ip { get; set; }
        public string? Port { get; set; }

        public override string ToString()
        {
            return $"connect {Ip}:{Port}";
        }
    }

}
