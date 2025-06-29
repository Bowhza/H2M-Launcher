using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Utilities
{
    public static class ServerConnectionDetailsExtensions
    {
        public static ServerConnectionDetails ToServerConnectionDetails(this IServerConnectionDetails value)
            => new(value.Ip, value.Port);
    }
}