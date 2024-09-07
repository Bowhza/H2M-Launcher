using System.Collections.Concurrent;
using System.Net;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;

using H2MLauncher.Core.Models;

using Haukcode.HighResolutionTimer;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class GameServerCommunicationService<TServer> : IAsyncDisposable
        where TServer : IServerConnectionDetails
    {
        private readonly ConcurrentDictionary<IPEndPoint, List<Request>> _queuedRequests = [];
        private GameServerCommunication? _gameServerCommunication;
        private readonly List<IDisposable> _registrations = [];
        private readonly List<CommandRegistration> _commandRegistrations = [];

        private readonly IEndpointResolver _endpointResolver;
        private readonly ILogger<GameServerCommunicationService<TServer>> _logger;

        private const int MIN_REQUEST_DELAY = 1;
        private const int MAX_PARALLEL_RESOLVE = 40;

        public GameServerCommunicationService(ILogger<GameServerCommunicationService<TServer>> logger, IEndpointResolver endpointResolver)
        {
            _logger = logger;
            _endpointResolver = endpointResolver;

            StartCommunication();
        }

        protected readonly record struct Request(CommandMessage Message, TServer Server)
        {
            public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
            public TaskCompletionSource<ReceivedCommandMessage>? ResponseCompletionSource { get; init; }
        }

        protected readonly record struct Response(Request Request)
        {
            public required CommandMessage Message { get; init; }
            public required DateTimeOffset Timestamp { get; init; }
            public required IPEndPoint RemoteEndPoint { get; init; }
        }

        private readonly struct CommandRegistration : IDisposable
        {
            private readonly IDisposable _communicationRegistration;
            public ICommand Command { get; init; }

            public CommandRegistration(ICommand command, IDisposable communicationRegistration) : this()
            {
                Command = command;
                _communicationRegistration = communicationRegistration;
            }

            public void Dispose()
            {
                _communicationRegistration.Dispose();
            }
        }


        /// <summary>
        /// Start game server communication.
        /// </summary>
        public void StartCommunication()
        {
            _gameServerCommunication = new();
        }

        protected interface ICommand
        {
            string RequestCommand { get; }

            string ResponseCommand { get; }

            CommandMessage CreateMessage();
        }

        protected interface ICommand<TResponse> : ICommand
        {
            TResponse? ParseResponse(Response response);
        }

        class GetInfoCommand : ICommand<GameServerInfo>
        {
            public string RequestCommand => "getinfo";

            public string ResponseCommand => "inforesponse";

            public CommandMessage CreateMessage()
            {
                return new CommandMessage(RequestCommand);
            }

            public GameServerInfo? ParseResponse(Response response)
            {
                try
                {
                    // Parse info string
                    InfoString info = new(response.Message.Data);

                    string? dedicated = info.Get("dedicated");
                    if (dedicated != "1")
                    {
                        return null;
                    }

                    string? svRunning = info.Get("sv_running");
                    if (svRunning != "1")
                    {
                        return null;
                    }

                    GameServerInfo serverInfo = new()
                    {
                        Address = response.RemoteEndPoint,
                        HostName = info.Get("hostname") ?? "",
                        MapName = info.Get("mapname") ?? "",
                        GameType = info.Get("gametype") ?? "",
                        ModName = info.Get("fs_game") ?? "",
                        PlayMode = info.Get("playmode") ?? "Unknown",
                        Clients = int.Parse(info.Get("clients") ?? "0"),
                        MaxClients = int.Parse(info.Get("sv_maxclients") ?? "0"),
                        Bots = int.Parse(info.Get("bots") ?? "0"),
                        Ping = Math.Min((int)(response.Timestamp - response.Request.Timestamp).TotalMilliseconds, 999),
                        IsPrivate = info.Get("isPrivate") == "1"
                    };

                    return serverInfo;
                }
                catch
                {
                    return null;
                }
            }
        }

        protected virtual IDisposable RegisterCommand<TResponse>(
            ICommand<TResponse> command,
            Action<Response, TResponse> onResponse,
            Predicate<ReceivedCommandMessage>? messageFilter = null,
            bool handleFirstOnly = true)
        {
            // Register info response command handler
            IDisposable handlerRegistration = _gameServerCommunication!.On(command.ResponseCommand,
                QueuedHandler(command, onResponse, messageFilter, handleFirstOnly));
            _registrations.Add(handlerRegistration);

            CommandRegistration reg = new(command, handlerRegistration);
            _commandRegistrations.Add(reg);

            return Disposable.Create(() =>
            {
                handlerRegistration.Dispose();
                _commandRegistrations.Remove(reg);
                _registrations.Remove(handlerRegistration);
            });
        }

        protected virtual Action<ReceivedCommandMessage> QueuedHandler<TResponse>(
            ICommand<TResponse> command,
            Action<Response, TResponse> onResponse,
            Predicate<ReceivedCommandMessage>? messageFilter = null,
            bool handleFirstOnly = true)
        {
            Predicate<ReceivedCommandMessage> handleMessage = new(messageFilter ?? ((_) => true));

            return (receivedMessage) =>
            {
                if (!handleMessage(receivedMessage))
                {
                    return;
                }

                if (!_queuedRequests.TryRemove(receivedMessage.RemoteEndPoint, out List<Request>? queuedRequests))
                {
                    // unknown remote endpoint
                    return;
                }

                IEnumerable<Request> matchingRequests = queuedRequests.Where(r => r.Message.CommandName.Equals(command.RequestCommand));
                if (!matchingRequests.Any())
                {
                    return;
                }

                if (handleFirstOnly)
                {
                    Request firstRequest = queuedRequests.FirstOrDefault(r => r.Message.CommandName.Equals(command.RequestCommand));
                    removeInterpretAndCallback(firstRequest);
                    return;
                }

                foreach (Request request in matchingRequests.ToList())
                {
                    removeInterpretAndCallback(request);
                }

                void removeInterpretAndCallback(Request request)
                {
                    if (queuedRequests.Remove(request))
                    {
                        request.ResponseCompletionSource?.TrySetResult(receivedMessage);

                        Response originalResponse = CreateResponse(request, receivedMessage);
                        TResponse? response = command.ParseResponse(originalResponse);
                        if (response is not null)
                        {
                            onResponse(originalResponse, response);
                        }
                    }
                }
            };
        }

        private static Response CreateResponse(Request request, ReceivedCommandMessage receivedMessage)
        {
            return new(request)
            {
                Message = receivedMessage.Message,
                Timestamp = receivedMessage.Timestamp,
                RemoteEndPoint = receivedMessage.RemoteEndPoint
            };
        }

        public Task<GameServerInfo?> GetInfoAsync(TServer server, CancellationToken cancellationToken)
        {
            return RequestCommandAsync(server, new GetInfoCommand(), cancellationToken);
        }

        /// <summary>
        /// Send info requests to all given game servers.
        /// </summary>
        public Task GetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer>> onInfoResponse,
            CancellationToken cancellationToken)
        {
            return RequestCommandAsync(servers, new GetInfoCommand(), (r, serverInfo) =>
                onInfoResponse(new ServerInfoEventArgs<TServer>()
                {
                    Server = r.Request.Server,
                    ServerInfo = serverInfo
                }), cancellationToken);
        }


        protected async Task<TResponse?> RequestCommandAsync<TResponse>(
            TServer server, ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            CommandMessage message = command.CreateMessage();

            Response? response = await RequestAsync(server, message, cancellationToken).ConfigureAwait(false);
            if (!response.HasValue)
            {
                return default;
            }

            return command.ParseResponse(response.Value);
        }

        /// <summary>
        /// Send requests to all given game servers.
        /// </summary>
        protected async Task RequestCommandAsync<TResponse>(
            IEnumerable<TServer> servers,
            ICommand<TResponse> command,
            Action<Response, TResponse> onResponse,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid timer allocation
                return;
            }

            // only invoke the callback for responses from the given servers
            Predicate<ReceivedCommandMessage> msgFilter = (msg) => endpointServerMap.ContainsKey(msg.RemoteEndPoint);

            // Register temporary command handler            
            IDisposable commandRegistration = RegisterCommand(command, onResponse, msgFilter, false);

            // Unregister when cancellation triggered
            cancellationToken.UnsafeRegister((state) => ((IDisposable?)state)?.Dispose(), commandRegistration);

            // Create reusable message
            CommandMessage message = command.CreateMessage();

            // Send message to all endpoints
            await SendRequestsAsync(endpointServerMap, (endpoint, server) => message, cancellationToken);
        }

        protected async Task<Response?> RequestAsync(
            TServer server, CommandMessage commandMessage, CancellationToken cancellationToken = default)
        {
            // create an endpoint to send to and receive from
            IPEndPoint? endpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
            if (endpoint is null)
            {
                return null;
            }

            TaskCompletionSource<ReceivedCommandMessage> tcs = new();

            // cancel task when token requests cancellation
            cancellationToken.UnsafeRegister((o) => ((TaskCompletionSource<ReceivedCommandMessage>)o!).TrySetCanceled(), tcs);

            Request request = new(commandMessage, server)
            {
                ResponseCompletionSource = tcs
            };

            bool success = await SendRequestInternalAsync(endpoint, request, cancellationToken);
            if (!success)
            {
                return null;
            }

            ReceivedCommandMessage receivedMessage = await tcs.Task.ConfigureAwait(false);
            Response response = CreateResponse(request, receivedMessage);

            return response;
        }

        /// <summary>
        /// Send requests to all given game servers.
        /// </summary>
        /// <returns>All requests that were sent successfully.</returns>
        protected async Task<IReadOnlyList<Request>> SendRequestsAsync(
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap,
            Func<IPEndPoint, TServer, CommandMessage> messageFactory,
            CancellationToken cancellationToken)
        {
            List<Request> successfulRequests = [];

            using HighResolutionTimer timer = new();
            timer.SetPeriod(MIN_REQUEST_DELAY);
            timer.Start();

            foreach (var (endpoint, server) in endpointServerMap)
            {
                CommandMessage message = messageFactory(endpoint, server);
                Request request = new(message, server);

                if (await SendRequestInternalAsync(endpoint, request, cancellationToken))
                {
                    successfulRequests.Add(request);
                }

                // wait for some bit. This is somehow necessary to receive all server responses.
                // NOTE: we use a high resolution timer because Task.Delay is too slow in release mode
                timer.WaitForTrigger();
            }

            return successfulRequests.AsReadOnly();
        }

        /// <summary>
        /// Send requests to all given game servers.
        /// </summary>
        /// <returns>All requests that were sent successfully.</returns>
        protected async Task<IReadOnlyList<Request>> SendRequestsAsync(
            IEnumerable<TServer> servers, Func<IPEndPoint, TServer, CommandMessage> messageFactory, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid timer allocation
                return [];
            }

            return await SendRequestsAsync(endpointServerMap, messageFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a single request to the game server.
        /// </summary>
        /// <returns>True, if the request was sent successfully.</returns>
        protected async Task<bool> SendRequestAsync(TServer server, CommandMessage commandMessage, CancellationToken cancellationToken)
        {
            // create an endpoint to send to and receive from
            IPEndPoint? serverEndpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
            if (serverEndpoint is null)
            {
                return false;
            }

            return await SendRequestInternalAsync(serverEndpoint, new Request(commandMessage, server), cancellationToken);
        }

        private async Task<bool> SendRequestInternalAsync(IPEndPoint serverEndpoint, Request request, CancellationToken cancellationToken)
        {
            if (_gameServerCommunication is null)
            {
                throw new InvalidOperationException("Communication is not started.");
            }

            AddToQueueCancelPreviousRequest(serverEndpoint, request, cancellationToken);

            try
            {
                // send 'getinfo' command
                await _gameServerCommunication.SendAsync(serverEndpoint, request.Message, cancellationToken: cancellationToken);

                return true;
            }
            catch
            {
                // failed to send info response (maybe server is not online)
                _queuedRequests.TryRemove(serverEndpoint, out _);

                return false;
            }
        }

        private void AddToQueueCancelPreviousRequest(IPEndPoint endpoint, Request request, CancellationToken cancellationToken)
        {
            var requests = _queuedRequests.GetOrAdd(endpoint, []);

            bool filter(Request x) =>
                x.Message.CommandName == request.Message.CommandName &&
                x.Message.Data == request.Message.Data &&
                x.Message.Separator == request.Message.Separator;

            // cancel previous operations
            foreach (var r in requests.Where(filter))
            {
                r.ResponseCompletionSource?.TrySetCanceled(cancellationToken);
            }

            // remove previous operation
            requests.RemoveAll(filter);
            requests.Add(request);

            cancellationToken.Register(() =>
            {
                if (_queuedRequests.TryGetValue(endpoint, out var list))
                {
                    list.Remove(request);
                }
            });
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