using System.Collections.Concurrent;
using System.Net;

using H2MLauncher.Core.Models;

using Haukcode.HighResolutionTimer;

namespace H2MLauncher.Core.Services
{
    public class GameServerCommunicationService : IAsyncDisposable
    {
        private readonly GameServerCommunication _gameServerCommunication = new();

        public async Task StartRetrievingGameServerInfo(
          IEnumerable<RaidMaxServer> servers,
          Action<RaidMaxServer, GameServerInfo> onServerInfoReceived,
          CancellationToken cancellationToken = default)
        {
            ConcurrentDictionary<IPEndPoint, DateTimeOffset> queuedServers = [];
            ConcurrentDictionary<IPEndPoint, RaidMaxServer> serverInfoMap = [];
            List<(GameServerInfo, RaidMaxServer)> result = [];

            //await using var gameServerCommunication = new GameServerCommunication();

            // handle info response
            var handlerReg = _gameServerCommunication.On("infoResponse", (e) =>
            {
                // get start timestamp
                if (!queuedServers.TryRemove(e.RemoteEndPoint, out var startTime))
                {
                    return;
                }

                // get server info
                if (!serverInfoMap.TryRemove(e.RemoteEndPoint, out var serverInfo))
                {
                    return;
                }

                try
                {
                    // Parse info string
                    var info = new InfoString(e.Data);

                    string? dedicated = info.Get("dedicated");
                    if (dedicated != "1")
                    {
                        return;
                    }

                    string? svRunning = info.Get("sv_running");
                    if (svRunning != "1")
                    {
                        return;
                    }

                    string? gameName = info.Get("gamename");
                    if (gameName != "H2M")
                    {
                        return;
                    }

                    GameServerInfo server = new()
                    {
                        Address = e.RemoteEndPoint,
                        HostName = info.Get("hostname") ?? "",
                        MapName = info.Get("mapname") ?? "",
                        GameType = info.Get("gametype") ?? "",
                        ModName = info.Get("fs_game") ?? "",
                        PlayMode = info.Get("playmode") ?? "Unknown",
                        Clients = int.Parse(info.Get("clients") ?? "0"),
                        MaxClients = int.Parse(info.Get("sv_maxclients") ?? "0"),
                        Bots = int.Parse(info.Get("bots") ?? "0"),
                        Ping = Math.Min((int)(e.Timestamp - startTime).TotalMilliseconds, 999),
                        IsPrivate = info.Get("isPrivate") == "1"
                    };

                    onServerInfoReceived?.Invoke(serverInfo, server);

                    result.Add((server, serverInfo));
                }

                catch
                {
                    // parsing error or smth
                }
            });

            // resolve host names in parallel
            await Parallel.ForEachAsync(
                servers,
                new ParallelOptions()
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = 40
                },
                async (server, token) =>
                {
                    // create an endpoint to send to and receive from
                    IPEndPoint endpoint;
                    if (IPAddress.TryParse(server.Ip, out var ipAddress))
                    {
                        // ip contains an actual ip address -> use that to create endpoint
                        endpoint = new(ipAddress.MapToIPv6(), server.Port);
                    }
                    else
                    {
                        // ip likely contains a hostname
                        try
                        {
                            // resolve ip addresses from hostname
                            var ipAddressList = await Dns.GetHostAddressesAsync(server.Ip, token);
                            var compatibleIp = ipAddressList.FirstOrDefault();
                            if (compatibleIp == null)
                            {
                                // could not resolve ip address
                                return;
                            }

                            endpoint = new IPEndPoint(compatibleIp.MapToIPv6(), server.Port);
                        }
                        catch
                        {
                            // invalid ip field
                            return;
                        }
                    }

                    serverInfoMap[endpoint] = server;
                });
            
            using HighResolutionTimer timer = new();
            timer.SetPeriod(2);
            timer.Start();

            foreach (var serverEndpoint in serverInfoMap.Keys)
            {
                // save start timestamp
                queuedServers[serverEndpoint] = DateTimeOffset.Now;
                try
                {                    
                    // send 'getinfo' command
                    await _gameServerCommunication.SendAsync(serverEndpoint, "getinfo", cancellationToken: cancellationToken).ConfigureAwait(false);
                    
                    // wait for some bit. This is somehow necessary to receive all server responses.
                    // NOTE: we use a high resolution timer because Task.Delay is too slow in release mode
                    timer.WaitForTrigger();
                }
                catch
                {
                    // failed to send info response (maybe server is not online)
                    queuedServers.TryRemove(serverEndpoint, out _);
                }
            }

            cancellationToken.Register(handlerReg.Dispose);
        }

        public ValueTask DisposeAsync()
        {
            return _gameServerCommunication.DisposeAsync();
        }
    }
}