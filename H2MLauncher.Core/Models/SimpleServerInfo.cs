using System.Diagnostics.CodeAnalysis;

namespace H2MLauncher.Core.Models
{
    public class SimpleServerInfo : ISimpleServerInfo
    {
        public required string ServerIp { get; set; }
        public required int ServerPort { get; set; }
        public required string ServerName { get; set; }


        // Explicit implementation for compatibility with stored settings
        string IServerConnectionDetails.Ip => ServerIp;
        int IServerConnectionDetails.Port => ServerPort;

        public SimpleServerInfo() { }

        [SetsRequiredMembers]
        public SimpleServerInfo(IServerConnectionDetails server, string serverName)
        {
            ServerIp = server.Ip;
            ServerPort = server.Port;
            ServerName = serverName;
        }
    }
}
