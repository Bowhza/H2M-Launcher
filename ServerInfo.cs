using System.Net.NetworkInformation;

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
        public string? Ping { get; set; } = "N/A";

        public override string ToString()
        {
            return $"connect {Ip}:{Port}";
        }
    }

    internal static class ServerInfoHelpers
    {

        public static async Task PingHostAsync(this ServerInfo serverInfo, CancellationToken cancellationToken)
        {
            Ping pinger = new();

            try
            {
                PingReply reply = await pinger.SendPingAsync(serverInfo.Ip!, TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
                if (reply.Status == IPStatus.Success)
                    serverInfo.Ping = reply.RoundtripTime.ToString();
            }
            catch (PingException)
            {
                // Discard PingExceptions
            }
            finally
            {
                pinger.Dispose();
            }
        }
    }
}
