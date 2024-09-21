namespace H2MLauncher.Core.Models
{
    public class JoinServerInfo : IFullServerConnectionDetails
    {
        public JoinServerInfo(string ip, int port)
        {
            Ip = ip;
            Port = port;
        }

        public string Ip { get; init; }
        public int Port { get; init; }
        public string? Password { get; init; }
        public string? ServerName { get; init; }
    }
}
