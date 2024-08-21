using System.Net.NetworkInformation;
using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public class ServerPingService
    {
        public async Task PingAsync(RaidMaxServer pingMaxServer, CancellationToken cancellationToken)
        {
            using Ping pinger = new();

            try
            {
                PingReply reply = await pinger.SendPingAsync(pingMaxServer.Ip, TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
                if (reply.Status == IPStatus.Success)
                    pingMaxServer.Ping = reply.RoundtripTime;
            }
            catch (PingException)
            {
                // Discard PingExceptions for now
            }
            finally
            {
                pinger.Dispose();
            }
        }
    }
}
