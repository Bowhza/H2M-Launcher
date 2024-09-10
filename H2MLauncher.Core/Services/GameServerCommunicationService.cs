using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

using H2MLauncher.Core.Models;

using Haukcode.HighResolutionTimer;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class GameServerCommunicationService<TServer> : IAsyncDisposable
        where TServer : IServerConnectionDetails
    {
        private readonly ConcurrentDictionary<IPEndPoint, InfoRequest> _queuedRequests = [];
        private GameServerCommunication? _gameServerCommunication;
        private readonly List<IDisposable> _registrations = [];

        private readonly IEndpointResolver _endpointResolver;
        private readonly ILogger<GameServerCommunicationService<TServer>> _logger;

        private const int MIN_REQUEST_DELAY = 1;
        private const int MAX_PARALLEL_RESOLVE = 40;
        private const string INFO_REQUEST = "getinfo";
        private const string INFO_RESPONSE = "inforesponse";

        public event EventHandler<ServerInfoEventArgs<TServer, GameServerInfo>>? ServerInfoReceived;
        public event EventHandler<ServerInfoEventArgs<TServer, GameServerStatus>>? ServerStatusReceived;

        public GameServerCommunicationService(ILogger<GameServerCommunicationService<TServer>> logger, IEndpointResolver endpointResolver)
        {
            _logger = logger;
            _endpointResolver = endpointResolver;

            StartCommunication();
        }

        private record struct InfoRequest(TServer Server)
        {
            public DateTimeOffset TimeStamp { get; init; } = DateTimeOffset.Now;
            public TaskCompletionSource<GameServerInfo>? InfoResponseCompletionSource { get; init; }
            public TaskCompletionSource<GameServerStatus>? StatusResponseCompletionSource { get; init; }
        }


        /// <summary>
        /// Start game server communication.
        /// </summary>
        public void StartCommunication()
        {
            _gameServerCommunication = new();

            // Register info response command handler
            IDisposable handlerRegistration = _gameServerCommunication.On(INFO_RESPONSE, OnInfoResponse);

            _registrations.Add(handlerRegistration);

            _registrations.Add(_gameServerCommunication.On("statusResponse", OnStatusResponse));
        }

        protected virtual void OnStatusResponse(GameServerCommunication.CommandEventArgs e)
        {
            try
            {
                if (!_queuedRequests.TryRemove(e.RemoteEndPoint, out InfoRequest queuedRequest))
                {
                    // unknown remote endpoint
                    return;
                }

                GameServerStatus status = new()
                {
                    Address = e.RemoteEndPoint
                };

                Console.WriteLine(e.Message);
                string[] lines = e.Data.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines.Skip(1))
                {
                    Match match = Regex.Match(line, @"(\d+) (\d+) ""(.*)""");
                    if (match.Groups.Count != 3 || match.Groups.Values.Any(g => !g.Success))
                    {
                        continue;
                    }

                    string[] splitted = line.Split(' ');
                    if (splitted.Length != 3)
                    {
                        continue;
                    }

                    int.TryParse(match.Groups[0].Value, out int score);
                    int.TryParse(match.Groups[1].Value, out int ping);
                    string playerName = match.Groups[2].Value;

                    status.Players.Add((score, ping, playerName));
                }
                // Try complete the request
                queuedRequest.StatusResponseCompletionSource?.TrySetResult(status);

                ServerStatusReceived?.Invoke(this, new()
                {
                    ServerInfo = status,
                    Server = queuedRequest.Server
                });
            }
            catch (Exception ex)
            {
                // parsing error or smth
                _logger.LogError(ex, "Error while parsing game server status response: {responseData}", e.Data);
            }
        }

        /// <summary>
        /// Handles 'infoResponse' messages.
        /// </summary>
        protected virtual void OnInfoResponse(GameServerCommunication.CommandEventArgs e)
        {
            try
            {
                if (!_queuedRequests.TryRemove(e.RemoteEndPoint, out InfoRequest queuedRequest))
                {
                    // unknown remote endpoint
                    return;
                }

                // Parse info string
                InfoString info = new(e.Data);

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

        public async Task<string?> RequestCommandAsync(TServer server, string command, CancellationToken cancellationToken)
        {
            if (_gameServerCommunication is null)
            {
                throw new InvalidOperationException("Communication is not started.");
            }

            // create an endpoint to send to and receive from
            IPEndPoint? endpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
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
                await _gameServerCommunication.SendAsync(endpoint, command, cancellationToken: cancellationToken);

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
            IPEndPoint? endpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
            if (endpoint is null)
            {
                return null;
            }

            TaskCompletionSource<GameServerInfo> tcs = new();

            // cancel task when token requests cancellation
            cancellationToken.UnsafeRegister((o) => ((TaskCompletionSource<GameServerInfo>)o!).TrySetCanceled(), tcs);

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

        /// <summary>
        /// Send info requests to all given game servers.
        /// </summary>
        public async Task RequestServerInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid allocations
                return;
            }

            // subscribe to server info handlers
            ServerInfoReceived += onServerInfoReceived;
            cancellationToken.Register(() => ServerInfoReceived -= onServerInfoReceived);

            // start sending requests
            using HighResolutionTimer timer = new();
            timer.SetPeriod(MIN_REQUEST_DELAY);
            timer.Start();

            foreach (var (endpoint, server) in endpointServerMap)
            {
                InfoRequest request = new(server);

                _ = await SendInfoRequestInternalAsync(endpoint, request, cancellationToken);

                // wait for some bit. This is somehow necessary to receive all server responses.
                // NOTE: we use a high resolution timer because Task.Delay is too slow in release mode
                timer.WaitForTrigger();
            }

            void onServerInfoReceived(object? sender, ServerInfoEventArgs<TServer, GameServerInfo> args)
            {
                // only invoke the callback for responses from the given servers
                if (endpointServerMap.ContainsKey(args.ServerInfo.Address))
                {
                    onInfoResponse(args);
                }
            }
        }

        /// <summary>
        /// Send info requests to all given game servers.
        /// </summary>
        public async Task RequestStatusAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerStatus>> onStatusResponse,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid allocations
                return;
            }

            // subscribe to server info handlers
            ServerStatusReceived += onServerInfoReceived;
            cancellationToken.Register(() => ServerStatusReceived -= onServerInfoReceived);

            // start sending requests
            using HighResolutionTimer timer = new();
            timer.SetPeriod(MIN_REQUEST_DELAY);
            timer.Start();

            foreach (var (endpoint, server) in endpointServerMap)
            {
                InfoRequest request = new(server);

                _ = await SendStatusRequestInternalAsync(endpoint, request, cancellationToken);

                // wait for some bit. This is somehow necessary to receive all server responses.
                // NOTE: we use a high resolution timer because Task.Delay is too slow in release mode
                timer.WaitForTrigger();
            }

            void onServerInfoReceived(object? sender, ServerInfoEventArgs<TServer, GameServerStatus> args)
            {
                // only invoke the callback for responses from the given servers
                if (endpointServerMap.ContainsKey(args.ServerInfo.Address))
                {
                    onStatusResponse(args);
                }
            }
        }

        /// <summary>
        /// Send info requests to all given game servers.
        /// </summary>
        public async Task SendInfoRequestsAsync(IEnumerable<TServer> servers, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid timer allocation
                return;
            }

            using HighResolutionTimer timer = new();
            timer.SetPeriod(MIN_REQUEST_DELAY);
            timer.Start();

            foreach (var (endpoint, server) in endpointServerMap)
            {
                InfoRequest request = new(server);

                _ = await SendInfoRequestInternalAsync(endpoint, request, cancellationToken);

                // wait for some bit. This is somehow necessary to receive all server responses.
                // NOTE: we use a high resolution timer because Task.Delay is too slow in release mode
                timer.WaitForTrigger();
            }
        }

        /// <summary>
        /// Send a single info request to the game server.
        /// </summary>
        /// <returns>True, if the request was sent successfully.</returns>
        public async Task<bool> SendInfoRequestAsync(TServer server, CancellationToken cancellationToken)
        {
            // create an endpoint to send to and receive from
            IPEndPoint? serverEndpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
            if (serverEndpoint is null)
            {
                return false;
            }

            return await SendInfoRequestInternalAsync(serverEndpoint, new InfoRequest(server), cancellationToken);
        }

        private async Task<bool> SendStatusRequestInternalAsync(IPEndPoint serverEndpoint, InfoRequest request, CancellationToken cancellationToken)
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
                await _gameServerCommunication.SendAsync(serverEndpoint, "getstatus", cancellationToken: cancellationToken);

                return true;
            }
            catch
            {
                // failed to send info response (maybe server is not online)
                _queuedRequests.TryRemove(serverEndpoint, out _);

                return false;
            }
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
                await _gameServerCommunication.SendAsync(serverEndpoint, INFO_REQUEST, cancellationToken: cancellationToken);

                return true;
            }
            catch
            {
                // failed to send info response (maybe server is not online)
                _queuedRequests.TryRemove(serverEndpoint, out _);

                return false;
            }
        }

        private void AddToQueueCancelPreviousRequest(IPEndPoint endpoint, InfoRequest request, CancellationToken cancellationToken)
        {
            if (!_queuedRequests.TryAdd(endpoint, request))
            {
                // cancel previous operation
                _queuedRequests.GetValueOrDefault(endpoint).InfoResponseCompletionSource?.TrySetCanceled(cancellationToken);
                _queuedRequests[endpoint] = request;
            }

            cancellationToken.Register(() => _queuedRequests.TryRemove(endpoint, out _));
        }

        /// <summary>
        /// Creates a dictionary of ip endpoints to servers by resolving the addresses in parallel and filtering out duplicates.
        /// </summary>
        private async Task<IReadOnlyDictionary<IPEndPoint, TServer>> CreateEndpointServerMap(
            IEnumerable<TServer> servers, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<IPEndPoint, TServer> endpointServerMap = [];

            // resolve host names in parallel
            await Parallel.ForEachAsync(
                servers,
                new ParallelOptions()
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = MAX_PARALLEL_RESOLVE
                },
                async (server, token) =>
                {
                    // create an endpoint to send to and receive from
                    IPEndPoint? endpoint = await _endpointResolver.GetEndpointAsync(server, token);
                    if (endpoint != null)
                    {
                        // filter out duplicates
                        if (!endpointServerMap.TryAdd(endpoint, server))
                        {
                            // duplicate
                        }
                    }
                });

            return endpointServerMap.AsReadOnly();
        }

        public ValueTask DisposeAsync()
        {
            _registrations.ForEach(reg => reg.Dispose());
            _registrations.Clear();

            return _gameServerCommunication?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}