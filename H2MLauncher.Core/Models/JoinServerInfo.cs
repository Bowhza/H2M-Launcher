namespace H2MLauncher.Core.Models
{
    public class JoinServerInfo : IFullServerConnectionDetails, ISimpleServerInfo
    {
        public JoinServerInfo(string ip, int port, string name)
        {
            Ip = ip;
            Port = port;
            ServerName = name;
        }

        public string Ip { get; init; }
        public int Port { get; init; }
        public string? Password { get; init; }
        public string ServerName { get; init; }
    }
}
