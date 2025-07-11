﻿using System.Collections.Concurrent;

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
        
        public bool TryGetServer(string ipAddress, int port, out GameServer? server)
        {            
            return _servers.TryGetValue((ipAddress, port), out server);
        }

        public GameServer? TryAddServer(string serverIp, int serverPort, string? serverName = null)
        {
            // get data for this server from the settings
            ServerData? data = _serverSettings.CurrentValue.ServerDataList.Find(s =>
                s.Ip == serverIp && s.Port == serverPort);

            // server does not have a queue yet, create new
            GameServer server = new()
            {
                Id = Guid.NewGuid().ToString(),
                ServerIp = serverIp,
                ServerPort = serverPort,
                ServerName = serverName ?? "",
                PrivilegedSlots = data?.PrivilegedSlots ?? 0,
            };

            if (_servers.TryAdd((serverIp, serverPort), server))
            {
                return server;
            }

            return null;
        }

        public GameServer GetOrAddServer(string serverIp, int serverPort, string? serverName = null)
        {
            return _servers.GetOrAdd((serverIp, serverPort), (_) =>
            {
                // get data for this server from the settings
                ServerData? data = _serverSettings.CurrentValue.ServerDataList.Find(s =>
                    s.Ip == serverIp && s.Port == serverPort);

                // server does not have a queue yet, create new
                GameServer server = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    ServerIp = serverIp,
                    ServerPort = serverPort,
                    ServerName = serverName ?? "",
                    PrivilegedSlots = data?.PrivilegedSlots ?? 0,
                };

                return server;
            });
        }
    }
}
