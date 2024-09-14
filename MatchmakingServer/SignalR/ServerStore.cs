using System.Collections.Concurrent;

using H2MLauncher.Core.Models;

using Microsoft.Extensions.Options;

namespace MatchmakingServer.SignalR
{
    public sealed class ServerStore
    {
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;

        private readonly ConcurrentDictionary<(string ip, int port), GameServer> _servers = new();

        public IDictionary<(string ip, int port), GameServer> Servers { get; }

        public ServerStore(IOptionsMonitor<ServerSettings> serverSettings)
        {
            Servers = _servers;
            _serverSettings = serverSettings;
        }

        public GameServer? TryAddServer(string serverIp, int serverPort, string instanceId = "")
        {
            // get data for this server from the settings
            ServerData? data = _serverSettings.CurrentValue.ServerDataList.Find(s =>
                s.Ip == serverIp && s.Port == serverPort);

            // server does not have a queue yet, create new
            GameServer server = new(instanceId)
            {
                ServerIp = serverIp,
                ServerPort = serverPort,
                PrivilegedSlots = data?.PrivilegedSlots ?? 0,
            };

            if (_servers.TryAdd((serverIp, serverPort), server))
            {
                return server;
            }

            return null;
        }

        public GameServer GetOrAddServer(string serverIp, int serverPort, string instanceId = "")
        {
            return _servers.GetOrAdd((serverIp, serverPort), (_) =>
            {
                // get data for this server from the settings
                ServerData? data = _serverSettings.CurrentValue.ServerDataList.Find(s =>
                    s.Ip == serverIp && s.Port == serverPort);

                // server does not have a queue yet, create new
                GameServer server = new(instanceId)
                {
                    ServerIp = serverIp,
                    ServerPort = serverPort,
                    PrivilegedSlots = data?.PrivilegedSlots ?? 0,
                };

                return server;
            });
        }
    }
}
