using System.Collections.Concurrent;
using System.Net;

using H2MLauncher.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace H2MLauncher.Core.Services
{
    public interface IServerConnectionDetails
    {
        string Ip { get; }

        int Port { get; }
    }

    public class ServerInfoEventArgs<TServer> : EventArgs
         where TServer : IServerConnectionDetails
    {
        public required GameServerInfo ServerInfo { get; init; }

        public required TServer Server { get; init; }
    }

    public class GameServerCommunicationService<TServer> : IAsyncDisposable
        where TServer : IServerConnectionDetails
    {
        private readonly ConcurrentDictionary<IPEndPoint, InfoRequest> _queuedServers = [];

        private GameServerCommunication? _gameServerCommunication;
        private readonly List<IDisposable> _registrations = [];

        private readonly ILogger<GameServerCommunicationService<TServer>> _logger;
        private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

        private const int MIN_REQUEST_DELAY = 1;

        public event EventHandler<ServerInfoEventArgs<TServer>>? ServerInfoReceived;

        public GameServerCommunicationService(ILogger<GameServerCommunicationService<TServer>> logger)
        {
            _logger = logger;

            StartCommunication();
        }

        private record struct InfoRequest(TServer Server)
        {
            public DateTimeOffset TimeStamp { get; init; } = DateTimeOffset.Now;
            public TaskCompletionSource<GameServerInfo>? InfoResponseCompletionSource { get; init; }
        }

        private record struct IpEndpointCacheKey(string IpOrHostName, int Port) { }


        /// <summary>
        /// Start game server communication.
        /// </summary>
        public void StartCommunication()
        {
            _gameServerCommunication = new();

            // Register info response command handler
            var handlerRegistration = _gameServerCommunication.On("infoResponse", OnInfoResponse);

            _registrations.Add(handlerRegistration);
        }

        /// <summary>
        /// Handles 'infoResponse' messages.
        /// </summary>
        protected virtual void OnInfoResponse(GameServerCommunication.CommandEventArgs e)
        {
            try
            {
                if (!_queuedServers.TryRemove(e.RemoteEndPoint, out var queuedRequest))
                {
                    // unknown remote endpoint
                    return;
                }

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

                GameServerInfo serverInfo = new()
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
                    Ping = Math.Min((int)(e.Timestamp - queuedRequest.TimeStamp).TotalMilliseconds, 999),
                    IsPrivate = info.Get("isPrivate") == "1"
                };

                // Try complete the request
                queuedRequest.InfoResponseCompletionSource?.TrySetResult(serverInfo);

                ServerInfoReceived?.Invoke(this, new()
                {
                    ServerInfo = serverInfo,
                    Server = queuedRequest.Server
                });
            }
            catch (Exception ex)
            {
                // parsing error or smth
                _logger.LogError(ex, "Error while parsing game server info response: {responseData}", e.Data);
            }
        }

        /// <summary>
        /// Tries to resolve the <see cref="IPEndPoint"/> of the given <paramref name="server"/> using it's hostname.
        /// </summary>
        private async Task<IPEndPoint?> ResolveEndpointAsync(TServer server, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Resolving endpoint for server {Server}...", server);

            // ip likely contains a hostname
            try
            {
                // resolve ip addresses from hostname
                var ipAddressList = await Dns.GetHostAddressesAsync(server.Ip, cancellationToken);
                var compatibleIp = ipAddressList.FirstOrDefault();
                if (compatibleIp == null)
                {
                    // could not resolve ip address
                    _logger.LogDebug("Not IP address found for {HostName}", server.Ip);
                    return null;
                }

                _logger.LogDebug("Found IP address for {HostName}: {IP} ", server.Ip, compatibleIp);
                return new IPEndPoint(compatibleIp.MapToIPv6(), server.Port);
            }
            catch (Exception ex)
            {
                // invalid ip field
                _logger.LogWarning(ex, "Error while resolving endpoint for server {Server}", server);
                return null;
            }
        }

        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> for the given <paramref name="server"/> from the cache,
        /// or tries to create / resolve it when no valid cache entry is found.
        /// </summary>
        private Task<IPEndPoint?> GetOrResolveEndpointAsync(TServer server, CancellationToken cancellationToken)
        {
            return _memoryCache.GetOrCreateAsync(
                new IpEndpointCacheKey(server.Ip, server.Port),
                async (cacheEntry) =>
                {
                    if (IPAddress.TryParse(server.Ip, out var ipAddress))
                    {
                        cacheEntry.SlidingExpiration = TimeSpan.FromHours(10);

                        // ip contains an actual ip address -> use that to create endpoint
                        return new IPEndPoint(ipAddress.MapToIPv6(), server.Port);
                    }

                    var endpoint = await ResolveEndpointAsync(server, cancellationToken);
                    if (endpoint is null)
                    {
                        cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(120);
                        return null;
                    }

                    return endpoint;
                });
        }

        public async Task<string?> RequestCommandAsync(TServer server, string command, CancellationToken cancellationToken)
        {
            if (_gameServerCommunication is null)
            {
                throw new InvalidOperationException("Communication is not started.");
            }

            // create an endpoint to send to and receive from
            IPEndPoint? endpoint = await GetOrResolveEndpointAsync(server, cancellationToken);
            if (endpoint is null)
            {
                return null;
            }

            TaskCompletionSource<string> tcs = new();

            // cancel task when token requests cancellation
            cancellationToken.UnsafeRegister((o) => ((TaskCompletionSource<GameServerInfo>)o!).TrySetCanceled(), tcs);

            IDisposable commandHandlerRegistration = _gameServerCommunication.On(command, e =>
            {
                tcs.TrySetResult(e.Data);
            });

            try
            {
                // send 'getinfo' command
                await _gameServerCommunication.SendAsync(endpoint, "getinfo", cancellationToken: cancellationToken);

                return await tcs.Task.ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
            finally
            {
                commandHandlerRegistration.Dispose();
            }
        }

        public async Task<GameServerInfo?> RequestServerInfoAsync(TServer server, CancellationToken cancellationToken)
        {
            // create an endpoint to send to and receive from
            IPEndPoint? endpoint = await GetOrResolveEndpointAsync(server, cancellationToken);
            if (endpoint is null)
            {
                return null;
            }

            TaskCompletionSource<GameServerInfo> tcs = new();

            // cancel task when token requests cancellation
            cancellationToken.UnsafeRegister((o) => ((TaskCompletionSource<GameServerInfo>)o!).TrySetCanceled(), tcs);
            cancellationToken.Register(() =>
            {
                // remove queued server request
                _queuedServers.TryRemove(endpoint, out _);
            });

            InfoRequest request = new(server)
            {
                InfoResponseCompletionSource = tcs
            };

            bool success = await SendInfoRequestInternalAsync(endpoint, request, cancellationToken);
            if (!success)
            {
                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        private void AddToQueueCancelPreviousRequest(IPEndPoint endpoint, InfoRequest request, CancellationToken cancellationToken)
        {
            if (!_queuedServers.TryAdd(endpoint, request))
            {
                // cancel previous operation
                _queuedServers.GetValueOrDefault(endpoint).InfoResponseCompletionSource?.TrySetCanceled(cancellationToken);
                _queuedServers[endpoint] = request;
            }
        }

        /// <summary>
        /// Send info requests to all given game servers.
        /// </summary>
        public async Task SendInfoRequestsAsync(IEnumerable<TServer> servers, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<IPEndPoint, TServer> endpointServerMap = [];

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
                    IPEndPoint? endpoint = await GetOrResolveEndpointAsync(server, token);
                    if (endpoint != null)
                    {
                        // filter out duplicates
                        if (!endpointServerMap.TryAdd(endpoint, server))
                        {
                            // duplicate
                        }
                    }
                });

            foreach (var (endpoint, server) in endpointServerMap)
            {
                InfoRequest request = new(server);

                _ = await SendInfoRequestInternalAsync(endpoint, request, cancellationToken);

                // wait for some bit. This is somehow necessary to receive all server responses.
                await Task.Delay(MIN_REQUEST_DELAY + 3, cancellationToken);
            }
        }

        /// <summary>
        /// Send a single info request to the game server.
        /// </summary>
        /// <returns>True, if the request was sent successfully.</returns>
        public async Task<bool> SendInfoRequestAsync(TServer server, CancellationToken cancellationToken)
        {
            // create an endpoint to send to and receive from
            var serverEndpoint = await GetOrResolveEndpointAsync(server, cancellationToken);
            if (serverEndpoint is null)
            {
                return false;
            }

            return await SendInfoRequestInternalAsync(serverEndpoint, new InfoRequest(server), cancellationToken);
        }

        private async Task<bool> SendInfoRequestInternalAsync(IPEndPoint serverEndpoint, InfoRequest request, CancellationToken cancellationToken)
        {
            if (_gameServerCommunication is null)
            {
                throw new InvalidOperationException("Communication is not started.");
            }

            // save start timestamp to calculate ping
            AddToQueueCancelPreviousRequest(serverEndpoint, request, cancellationToken);

            try
            {
                // send 'getinfo' command
                await _gameServerCommunication.SendAsync(serverEndpoint, "getinfo", cancellationToken: cancellationToken);

                return true;
            }
            catch
            {
                // failed to send info response (maybe server is not online)
                _queuedServers.TryRemove(serverEndpoint, out _);

                return false;
            }
        }
        public ValueTask DisposeAsync()
        {
            _registrations.ForEach(reg => reg.Dispose());
            _registrations.Clear();

            return _gameServerCommunication?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}