using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

using ConcurrentCollections;

using H2MLauncher.Core.Models;

using Haukcode.HighResolutionTimer;

using Microsoft.Extensions.Logging;


namespace H2MLauncher.Core.Services
{
    public partial class GameServerCommunicationService<TServer> : IAsyncDisposable
        where TServer : IServerConnectionDetails
    {
        private readonly ConcurrentDictionary<IPEndPoint, ConcurrentHashSet<Request>> _queuedRequests = [];
        private GameServerCommunication? _gameServerCommunication;
        private readonly List<IDisposable> _registrations = [];

        private readonly IEndpointResolver _endpointResolver;
        private readonly ILogger<GameServerCommunicationService<TServer>> _logger;

        private const int MIN_REQUEST_DELAY = 1;
        private const int MAX_PARALLEL_RESOLVE = 40;
        private const int REQUEST_TIMEOUT_IN_MS = 10000;

        public GameServerCommunicationService(ILogger<GameServerCommunicationService<TServer>> logger, IEndpointResolver endpointResolver)
        {
            _logger = logger;
            _endpointResolver = endpointResolver;

            StartCommunication();
        }

        protected enum RequestState
        {
            Created,
            Canceled,
            Waiting,
            Completed,
            Error
        }

        protected class Request : IDisposable
        {
            private readonly CancellationTokenSource _cancellation = new();
            private int _isCanceled = 0;
            private bool _isWaiting = false;

            public CommandMessage Message { get; }

            public TServer Server { get; }

            /// <summary>
            /// When this request was sent.
            /// </summary>
            public DateTimeOffset Timestamp { get; internal set; } = DateTimeOffset.Now;

            /// <summary>
            /// Holds a task that is completed when a matching response is received, or canceled when
            /// the request is canceled or timed out.
            /// </summary>
            internal TaskCompletionSource<ReceivedCommandMessage> ResponseCompletionSource { get; init; } = new();

            public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(REQUEST_TIMEOUT_IN_MS);

            /// <summary>
            /// Used to cancel the request timeout after the request is completed or canceled.
            /// </summary>
            public CancellationToken CancellationToken => _cancellation.Token;

            public RequestState State
            {
                get
                {
                    return ResponseCompletionSource.Task.Status switch
                    {
                        TaskStatus.Faulted => RequestState.Error,
                        TaskStatus.Canceled => RequestState.Canceled,
                        TaskStatus.RanToCompletion => RequestState.Completed,
                        _ => _isWaiting ? RequestState.Waiting : RequestState.Created
                    };
                }
            }

            public Request(CommandMessage message, TServer server)
            {
                Message = message;
                Server = server;

                CancellationToken.Register(() =>
                {
                    // make sure response task is canceled
                    ResponseCompletionSource.TrySetCanceled(CancellationToken);
                });
            }

            internal void Activate()
            {
                if (_isCanceled == 1) return;

                _cancellation.CancelAfter(Timeout);
                _isWaiting = true;
            }

            public bool TryCancel()
            {
                if (Interlocked.Exchange(ref _isCanceled, 1) == 0)
                {
                    _cancellation.Cancel();

                    // cancellation has to be disposed!
                    _cancellation.Dispose();

                    return true;
                }

                return false;
            }

            public void Dispose()
            {
                _isCanceled = 1;
                _cancellation.Dispose();
            }

            public override string ToString()
            {
                return $"'{Message.CommandName}' - {State} ({Timestamp})";
            }
        }

        protected readonly record struct Response(Request Request)
        {
            public required CommandMessage Message { get; init; }
            public required string RawMessage { get; init; }
            public required DateTimeOffset Timestamp { get; init; }
            public required IPEndPoint RemoteEndPoint { get; init; }
        }


        /// <summary>
        /// Start game server communication.
        /// </summary>
        public void StartCommunication()
        {
            _gameServerCommunication = new();

            _registrations.Add(RegisterQueuedCommandHandler(GetInfoCommand.CommandName, GetInfoCommand.ResponseCommandName));
            _registrations.Add(RegisterQueuedCommandHandler(GetStatusCommand.CommandName, GetStatusCommand.ResponseCommandName));
        }

        protected interface ICommand
        {
            static abstract string CommandName { get; }

            CommandMessage CreateMessage();
        }

        protected interface ICommand<TResponse> : ICommand
        {
            static abstract string ResponseCommandName { get; }

            TResponse? ParseResponse(Response response);
        }

        class GetInfoCommand : ICommand<GameServerInfo>
        {
            public static string CommandName => "getinfo";
            public static string ResponseCommandName => "inforesponse";

            public CommandMessage CreateMessage()
            {
                return new CommandMessage(CommandName);
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

        partial class GetStatusCommand : ICommand<GameServerStatus>
        {
            public static string CommandName => "getstatus";

            public static string ResponseCommandName => "statusResponse";

            public CommandMessage CreateMessage()
            {
                return new CommandMessage(CommandName);
            }

            public GameServerStatus? ParseResponse(Response response)
            {
                try
                {
                    GameServerStatus status = new()
                    {
                        Address = response.RemoteEndPoint
                    };

                    string[] lines = response.Message.Data.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines.Skip(1))
                    {
                        Match match = PlayerStatusLineRegex().Match(line);
                        if (match.Groups.Count < 4 || match.Groups.Values.Any(g => !g.Success))
                        {
                            continue;
                        }

                        string[] splitted = line.Split(' ');
                        if (splitted.Length != 3)
                        {
                            continue;
                        }

                        int.TryParse(match.Groups[1].Value, out int score);
                        int.TryParse(match.Groups[2].Value, out int ping);
                        string playerName = match.Groups[3].Value;

                        status.Players.Add((score, ping, playerName));
                    }

                    return status;
                }
                catch
                {
                    return null;
                }
            }

            [GeneratedRegex(@"(\d+) (\d+) ""(.*)""")]
            private static partial Regex PlayerStatusLineRegex();
        }

        protected IDisposable RegisterQueuedCommandHandler(string requestCommandName, string responseCommandName,
            Action<Response>? onResponse = null,
            Predicate<ReceivedCommandMessage>? messageFilter = null,
            bool handleFirstOnly = true)
        {
            // Register info response command handler
            IDisposable handlerRegistration = _gameServerCommunication!.On(responseCommandName,
                QueuedRequestHandler(requestCommandName, onResponse, messageFilter, handleFirstOnly));
            _registrations.Add(handlerRegistration);

            return handlerRegistration;
        }

        /// <summary>
        /// A handler that handles incoming messages for queued requests with the given <paramref name="requestCommandName"/>. <br/>
        /// Sets the <see cref="Request.ResponseCompletionSource"/> when a matching command message is received
        /// and calls the <paramref name="onResponse"/> callback.
        /// </summary>
        /// <param name="requestCommandName"></param>
        /// <param name="onResponse"></param>
        /// <param name="messageFilter"></param>
        /// <param name="handleFirstOnly"></param>
        /// <returns></returns>
        protected virtual Action<ReceivedCommandMessage> QueuedRequestHandler(
            string requestCommandName,
            Action<Response>? onResponse = null,
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

                if (!_queuedRequests.TryGetValue(receivedMessage.RemoteEndPoint, out var queuedRequests))
                {
                    // unknown remote endpoint
                    return;
                }

                IEnumerable<Request> matchingRequests = queuedRequests.Where(r => r.Message.CommandName.Equals(requestCommandName));
                if (!matchingRequests.Any())
                {
                    return;
                }

                if (handleFirstOnly)
                {
                    Request firstRequest = matchingRequests.First();
                    removeInterpretAndCallback(firstRequest);
                    return;
                }

                foreach (Request request in matchingRequests.ToList())
                {
                    removeInterpretAndCallback(request);
                }

                void removeInterpretAndCallback(Request request)
                {
                    if (queuedRequests.TryRemove(request))
                    {
                        request.ResponseCompletionSource?.TrySetResult(receivedMessage);
                        if (onResponse is not null)
                        {
                            Response response = CreateResponse(request, receivedMessage);
                            onResponse(response);
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
                RawMessage = receivedMessage.RawMessage,
                Timestamp = receivedMessage.Timestamp,
                RemoteEndPoint = receivedMessage.RemoteEndPoint,
            };
        }

        #region Command specific request methods

        public async Task<GameServerInfo?> GetInfoAsync(TServer server, CancellationToken cancellationToken)
        {
            GetInfoCommand command = new();
            Response? response = await RequestAsync(server, command.CreateMessage(), cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.HasValue ? command.ParseResponse(response.Value) : null;
        }

        /// <summary>
        /// Send info requests to all given game servers.
        /// </summary>
        public async Task<IAsyncEnumerable<(TServer server, GameServerInfo? info)>> GetInfoAsync(
            IEnumerable<TServer> servers,
            bool sendSynchronously = false,
            int requestTimeoutInMs = REQUEST_TIMEOUT_IN_MS,
            CancellationToken cancellationToken = default)
        {
            GetInfoCommand command = new();
            IAsyncEnumerable<Response> responses = await RequestAsync(
                servers, command.CreateMessage(), sendSynchronously, false, requestTimeoutInMs, cancellationToken);

            return responses.Select(response => (response.Request.Server, command.ParseResponse(response)));
        }

        /// <summary>
        /// Sends info requests to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onInfoResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the sending process only.</param>
        /// <returns>A <see cref="IDisposable"/> to stop receiving requests when disposed.</returns>
        public async Task<IDisposable> SendGetInfoAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            CancellationToken cancellationToken = default)
        {
            GetInfoCommand command = new();

            (IDisposable stopReceiving, Task<Response[]> completion) = await SendRequestWithCallbackAsync(
                servers,
                command.CreateMessage(),
                (response) =>
                {
                    GameServerInfo? serverInfo = command.ParseResponse(response);
                    if (serverInfo is not null)
                    {
                        onInfoResponse(new ServerInfoEventArgs<TServer, GameServerInfo>()
                        {
                            Server = response.Request.Server,
                            ServerInfo = serverInfo
                        });
                    }
                }, requestTimeoutInMs: REQUEST_TIMEOUT_IN_MS, cancellationToken: cancellationToken);

            return stopReceiving;
        }

        /// <summary>
        /// Sends info requests to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onInfoResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the requests.</param>
        /// <returns>A task that completes when all requests are sent.</returns>
        public Task<Task> SendGetInfoAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            int timeoutInMs = 10000,
            CancellationToken cancellationToken = default)
        {
            GetInfoCommand command = new();

            Task<Task> wrappedTask = SendRequestWithCallbackAsync(
                servers,
                command.CreateMessage(),
                (response) =>
                {
                    GameServerInfo? serverInfo = command.ParseResponse(response);
                    if (serverInfo is not null)
                    {
                        onInfoResponse(new ServerInfoEventArgs<TServer, GameServerInfo>()
                        {
                            Server = response.Request.Server,
                            ServerInfo = serverInfo
                        });
                    }
                }, waitTillCompletion: false, timeoutInMs, cancellationToken);

            return wrappedTask;
        }

        /// <summary>
        /// Sends info requests to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onInfoResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the requests.</param>
        /// <returns>A task that completes when all requests are completed or timed out.</returns>
        public Task GetInfoAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            int timeoutInMs = 10000,
            CancellationToken cancellationToken = default)
        {
            GetInfoCommand command = new();

            Task<Task> wrappedTask = SendRequestWithCallbackAsync(
                servers,
                command.CreateMessage(),
                (response) =>
                {
                    GameServerInfo? serverInfo = command.ParseResponse(response);
                    if (serverInfo is not null)
                    {
                        onInfoResponse(new ServerInfoEventArgs<TServer, GameServerInfo>()
                        {
                            Server = response.Request.Server,
                            ServerInfo = serverInfo
                        });
                    }
                }, waitTillCompletion: false, timeoutInMs, cancellationToken);

            return wrappedTask.Unwrap();
        }

        public async Task<GameServerStatus?> GetStatusAsync(TServer server, CancellationToken cancellationToken)
        {
            GetStatusCommand command = new();
            Response? response = await RequestAsync(server, command.CreateMessage(), cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.HasValue ? command.ParseResponse(response.Value) : null;
        }

        /// <summary>
        /// Sends status requests to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onInfoResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the sending process only.</param>
        /// <returns>A <see cref="IDisposable"/> to stop receiving requests when disposed.</returns>
        public async Task<IDisposable> SendGetStatusAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerStatus>> onInfoResponse,
            CancellationToken cancellationToken = default)
        {
            GetStatusCommand command = new();

            (IDisposable stopReceiving, Task<Response[]> completion) = await SendRequestWithCallbackAsync(
                servers,
                command.CreateMessage(),
                (response) =>
                {
                    GameServerStatus? serverInfo = command.ParseResponse(response);
                    if (serverInfo is not null)
                    {
                        onInfoResponse(new ServerInfoEventArgs<TServer, GameServerStatus>()
                        {
                            Server = response.Request.Server,
                            ServerInfo = serverInfo
                        });
                    }
                }, requestTimeoutInMs: REQUEST_TIMEOUT_IN_MS, cancellationToken: cancellationToken);

            return stopReceiving;
        }

        /// <summary>
        /// Sends status requests to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onInfoResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the requests.</param>
        /// <returns>A task that completes when all requests are sent.</returns>
        public Task<Task> SendGetStatusAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerStatus>> onInfoResponse,
            int timeoutInMs = 10000,
            CancellationToken cancellationToken = default)
        {
            GetStatusCommand command = new();

            Task<Task> wrappedTask = SendRequestWithCallbackAsync(
                servers,
                command.CreateMessage(),
                (response) =>
                {
                    GameServerStatus? serverInfo = command.ParseResponse(response);
                    if (serverInfo is not null)
                    {
                        onInfoResponse(new ServerInfoEventArgs<TServer, GameServerStatus>()
                        {
                            Server = response.Request.Server,
                            ServerInfo = serverInfo
                        });
                    }
                }, waitTillCompletion: false, timeoutInMs, cancellationToken);

            return wrappedTask;
        }

        /// <summary>
        /// Sends status requests to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onInfoResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the requests.</param>
        /// <returns>A task that completes when all requests are completed or timed out.</returns>
        public Task GetStatusAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerStatus>> onInfoResponse,
            int timeoutInMs = 10000,
            CancellationToken cancellationToken = default)
        {
            GetStatusCommand command = new();

            Task<Task> wrappedTask = SendRequestWithCallbackAsync(
                servers,
                command.CreateMessage(),
                (response) =>
                {
                    GameServerStatus? serverInfo = command.ParseResponse(response);
                    if (serverInfo is not null)
                    {
                        onInfoResponse(new ServerInfoEventArgs<TServer, GameServerStatus>()
                        {
                            Server = response.Request.Server,
                            ServerInfo = serverInfo
                        });
                    }
                }, waitTillCompletion: false, timeoutInMs, cancellationToken);

            return wrappedTask.Unwrap();
        }

        /// <summary>
        /// Send status requests to all given game servers.
        /// </summary>
        public async Task<IAsyncEnumerable<(TServer server, GameServerStatus? status)>> GetStatusAsync(
            IEnumerable<TServer> servers,
            bool sendSynchronously = false,
            int requestTimeoutInMs = REQUEST_TIMEOUT_IN_MS,
            CancellationToken cancellationToken = default)
        {
            GetStatusCommand command = new();
            IAsyncEnumerable<Response> responses = await RequestAsync(
                servers, command.CreateMessage(), sendSynchronously, false, requestTimeoutInMs, cancellationToken);

            return responses.Select(response => (response.Request.Server, command.ParseResponse(response)));
        }

        #endregion

        /// <summary>
        /// Sends a request with the given <paramref name="commandMessage"/> to all <paramref name="servers"/>
        /// and asynchronously yields all responses.
        /// </summary>
        /// <param name="servers">The servers to send the messages to (in order).</param>
        /// <param name="commandMessage">The command message to send.</param>
        /// <param name="sendSynchronously">Whether to wait for all requests to be sent before returning the enumerable.</param>
        /// <param name="failImmediately">Whether to complete with an exception when a single request fails.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel sending the requests.</param>
        /// <returns>A task that represents the sending operation and upon completion returns an <see cref="IAsyncEnumerable{T}"/>
        /// that asynchronously yields the responses in the order they are received.</returns>
        protected async Task<IAsyncEnumerable<Response>> RequestAsync(
            IEnumerable<TServer> servers,
            CommandMessage commandMessage,
            bool sendSynchronously = false,
            bool failImmediately = false,
            int requestTimeoutInMs = REQUEST_TIMEOUT_IN_MS,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid timer allocation
                return AsyncEnumerable.Empty<Response>();
            }

            // use a channel to stream the responses in receive order
            Channel<Response> channel = Channel.CreateUnbounded<Response>();
            ChannelWriter<Response> writer = channel.Writer;
            List<Request> requests = new(endpointServerMap.Count);

            // Start a background task that creates and sends all the requests
            // and writes the responses to the channel.
            // Returns a task that will complete once all responses are received or canceled.
            Task<Task> sendTask = Task.Run<Task>(async () =>
            {
                List<Task> continuations = new(endpointServerMap.Count);
                CancellationTokenRegistration reg = default;
                try
                {
                    // cancel all the requests when enumerator is canceled
                    reg = cancellationToken.Register(() =>
                    {
                        foreach (Request request in requests)
                        {
                            request.TryCancel();
                        }
                    });

                    // Send message to all endpoints
                    var requestEnumerable = SendRequestsAsync(endpointServerMap, (_, _) => commandMessage, requestTimeoutInMs, cancellationToken);

                    await foreach (Request request in requestEnumerable.ConfigureAwait(false))
                    {
                        requests.Add(request); // keep track of sent requests for cancellation

                        // Map each request to a continuation that writes the response to the channel.
                        // (All continuations will complete successfully even when request is canceled)
                        continuations.Add(
                            request.ResponseCompletionSource.Task.ContinueWith(
                                t =>
                                {
                                    if (t.IsCompletedSuccessfully)
                                    {
                                        // when a request completes, write the response to the channel
                                        Response response = CreateResponse(request, t.Result);
                                        writer.TryWrite(response);
                                    }
                                    else if (failImmediately)
                                    {
                                        // propagate the exception
                                        writer.TryComplete(t.Exception);
                                    }
                                },
                                CancellationToken.None,
                                TaskContinuationOptions.ExecuteSynchronously,
                                TaskScheduler.Default));
                    }

                    // Complete the writer when all responses are received or canceled
                    return Task.WhenAll(continuations)
                               .ContinueWith(t => writer.TryComplete(t.Exception));
                }
                catch (Exception ex)
                {
                    // Propagate the error by completing the writer with it
                    return Task.FromResult(writer.TryComplete(ex));
                }
                finally
                {
                    reg.Dispose();
                }
            }, CancellationToken.None);

            if (sendSynchronously)
            {
                // Wait until all requests are sent before returning the responses
                await sendTask;
            }

            // Streamed responses.
            // Not passing token because it is only for sending.
            // Passing 'WithCancellation' to enumerable will trigger the cancellation regardless (idk how this magic works)
            return channel.Reader.ReadAllAsync(CancellationToken.None);
        }

        private static async IAsyncEnumerable<Response> ReadRequestsUntilThenCancel(
            IAsyncEnumerable<Response> enumerable,
            IReadOnlyList<Request> requests,
            TimeSpan readUntil,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellation = new(readUntil);
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);

            await using IAsyncEnumerator<Response> enumerator = enumerable.GetAsyncEnumerator(linkedCancellation.Token);

            while (true)
            {
                Response nextResponse;
                try
                {
                    if (!await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                    {
                        yield break;
                    }
                    nextResponse = enumerator.Current;
                }
                catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
                {
                    foreach (Request request in requests)
                    {
                        request.TryCancel();
                    }

                    yield break;
                }
                yield return nextResponse;
            }
        }

        private static async IAsyncEnumerable<Response> ReadRequestsUntilThenCancel(
            ChannelReader<Response> reader,
            IReadOnlyList<Request> requests,
            TimeSpan readUntil,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellation = new(readUntil);
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);

            await using IAsyncEnumerator<Response> enumerator = reader.ReadAllAsync().GetAsyncEnumerator(linkedCancellation.Token);

            while (true)
            {
                Response nextResponse;
                try
                {
                    if (!await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                    {
                        yield break;
                    }
                    nextResponse = enumerator.Current;
                }
                catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
                {
                    foreach (Request request in requests)
                    {
                        request.TryCancel();
                    }

                    yield break;
                }
                yield return nextResponse;
            }
        }

        /// <summary>
        /// Sends a request with the given <paramref name="commandMessage"/> to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onResponse"/> for each received response.
        /// </summary>
        /// <param name="timeoutInMs">A timeout after which all requests will be canceled, starting after all requests are sent.</param>
        /// <param name="cancellationToken">A cancellation token that cancels the requests.</param>
        /// <returns>A task that returns when all requests are sent, or responded or canceled (depending on <paramref name="waitTillCompletion"/>).</returns>
        protected async Task<Task> SendRequestWithCallbackAsync(
            IEnumerable<TServer> servers,
            CommandMessage commandMessage,
            Action<Response> onResponse,
            bool waitTillCompletion = true,
            int timeoutInMs = REQUEST_TIMEOUT_IN_MS,
            CancellationToken cancellationToken = default)
        {
            (IDisposable stopReceiving, Task<Response[]> completion) = await SendRequestWithCallbackAsync(
                servers,
                commandMessage,
                onResponse,
                REQUEST_TIMEOUT_IN_MS,
                cancellationToken);


            List<IDisposable> disposables = new(3);

            if (timeoutInMs < REQUEST_TIMEOUT_IN_MS)
            {
                // only use timeout if smaller than global timeout
                var timeoutCancellation = new CancellationTokenSource(timeoutInMs);
                var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token, cancellationToken);
                var registration = linkedCancellation.Token.Register(stopReceiving.Dispose);

                disposables.Add(timeoutCancellation);
                disposables.Add(linkedCancellation);
                disposables.Add(registration);
            }
            else
            {
                using var registration = cancellationToken.Register(stopReceiving.Dispose);
                disposables.Add(registration);
            }

            if (!waitTillCompletion)
            {
                // leave cancellations alive
                return completion;
            }

            try
            {
                // wait till all requests are completed (whether successfully or not)
                return Task.FromResult(await completion.ConfigureAwait(false));
            }
            finally
            {
                disposables.ForEach(d => d.Dispose());
            }
        }


        /// <summary>
        /// Sends a request with the given <paramref name="commandMessage"/> to all <paramref name="servers"/>
        /// and asynchronously invokes <paramref name="onResponse"/> for each received response.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that cancels the sending process only.</param>
        /// <returns>A task that represents the sending operation and upon completion returns another task that completes
        /// when all requests are responded or canceled, and a <see cref="IDisposable"/> to stop receiving requests..</returns>
        protected async Task<(IDisposable stopReceiving, Task<Response[]> completion)> SendRequestWithCallbackAsync(
            IEnumerable<TServer> servers,
            CommandMessage commandMessage,
            Action<Response> onResponse,
            int requestTimeoutInMs = REQUEST_TIMEOUT_IN_MS,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid timer allocation
                return (Disposable.Empty, Task.FromResult<Response[]>([]));
            }

            List<Request> requests = new(endpointServerMap.Count);
            List<Task<Response>> continuations = new(endpointServerMap.Count);

            try
            {
                // Send message to all endpoints
                var sendingRequests = SendRequestsAsync(endpointServerMap, (_, _) => commandMessage, requestTimeoutInMs, cancellationToken);
                await foreach (Request request in sendingRequests.ConfigureAwait(false))
                {
                    requests.Add(request);

                    // Map each request to a continuation that invokes the response callback
                    continuations.Add(
                        request.ResponseCompletionSource.Task.ContinueWith(
                            t =>
                            {
                                // when a request completes, write the response to the channel
                                Response response = CreateResponse(request, t.Result);
                                onResponse(response);
                                return response;
                            },
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion,
                            TaskScheduler.Default));
                }
            }
            catch
            {
                // Cancel all requests sent until now
                requests.ForEach(r => r.TryCancel());
                throw;
            }

            // Disposable to stop reveiving requests
            IDisposable stopReceiving = Disposable.Create(() => requests.ForEach(r => r.TryCancel()));

            // Return task that completes when all responses are received or canceled
            Task<Response[]> allCompleted = Task.WhenAll(continuations);

            return (stopReceiving, allCompleted);
        }

        /// <summary>
        /// Sends a request with the <paramref name="commandMessage"/> to the <paramref name="server"/>
        /// and asynchronously waits for a response.
        /// </summary>
        /// <param name="server">The server to send the request to.</param>
        /// <param name="commandMessage">The message to send.</param>
        /// <param name="cancellationToken">Cancels the entire request, including waiting for a response.</param>
        /// <returns>A task representing the asynchronous operation of waiting for the response. 
        /// Will be faulted after the request timeout.</returns>
        /// <exception cref="TimeoutException">When the request timeout is exceeded.</exception>
        protected async Task<Response?> RequestAsync(
            TServer server, CommandMessage commandMessage, int requestTimeoutInMs = REQUEST_TIMEOUT_IN_MS, CancellationToken cancellationToken = default)
        {
            // create an endpoint to send to and receive from
            IPEndPoint? endpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
            if (endpoint is null)
            {
                return null;
            }

            Request request = new(commandMessage, server)
            {
                Timeout = TimeSpan.FromMilliseconds(requestTimeoutInMs)
            };

            bool success = await SendRequestInternalAsync(endpoint, request, cancellationToken).ConfigureAwait(false);
            if (!success)
            {
                return null;
            }

            using CancellationTokenRegistration reg = cancellationToken.Register(() => request.TryCancel());

            ReceivedCommandMessage receivedMessage = await request.ResponseCompletionSource.Task.ConfigureAwait(false);
            Response response = CreateResponse(request, receivedMessage);

            return response;
        }

        /// <summary>
        /// Send requests to all given game server <paramref name="servers"/> using a <paramref name="messageFactory"/> to create messages.
        /// </summary>
        /// <returns>All requests that were sent successfully.</returns>
        protected async Task<IReadOnlyList<Request>> SendRequestsAsync(
            IEnumerable<TServer> servers,
            Func<IPEndPoint, TServer, CommandMessage> messageFactory,
            int requestTimeoutInMs,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap = await CreateEndpointServerMap(servers, cancellationToken);

            if (endpointServerMap.Count == 0)
            {
                // early return to avoid timer allocation
                return [];
            }

            return await SendRequestsAsync(endpointServerMap, messageFactory, requestTimeoutInMs, cancellationToken)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }


        /// <summary>
        /// Send requests to all servers in the <paramref name="endpointServerMap"/>.
        /// Uses a <paramref name="messageFactory"/> to create messages for each server.
        /// </summary>
        /// <returns>All requests that were sent successfully.</returns>
        protected async IAsyncEnumerable<Request> SendRequestsAsync(
            IReadOnlyDictionary<IPEndPoint, TServer> endpointServerMap,
            Func<IPEndPoint, TServer, CommandMessage> messageFactory,
            int requestTimeoutInMs = REQUEST_TIMEOUT_IN_MS,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using HighResolutionTimer timer = new();
            timer.SetPeriod(MIN_REQUEST_DELAY);
            timer.Start();

            foreach (var (endpoint, server) in endpointServerMap)
            {
                CommandMessage message = messageFactory(endpoint, server);
                Request request = new(message, server)
                {
                    Timeout = TimeSpan.FromMilliseconds(requestTimeoutInMs)
                };

                if (await SendRequestInternalAsync(endpoint, request, cancellationToken))
                {
                    yield return request;
                }

                // wait for some bit. This is somehow necessary to receive all server responses.
                // NOTE: we use a high resolution timer because Task.Delay is too slow in release mode
                timer.WaitForTrigger();
            }
        }

        /// <summary>
        /// Send a single request with the <paramref name="commandMessage"/> to the game server <paramref name="server"/>.
        /// </summary>
        /// <returns>True, if the request was sent successfully.</returns>
        protected async Task<Request?> SendRequestAsync(TServer server, CommandMessage commandMessage, CancellationToken cancellationToken)
        {
            // create an endpoint to send to and receive from
            IPEndPoint? serverEndpoint = await _endpointResolver.GetEndpointAsync(server, cancellationToken);
            if (serverEndpoint is null)
            {
                return null;
            }

            Request request = new(commandMessage, server);

            if (await SendRequestInternalAsync(serverEndpoint, request, cancellationToken).ConfigureAwait(false))
            {
                return request;
            }

            return null;
        }

        /// <summary>
        /// Adds the <paramref name="request"/> to the queue, cancels equal previous requests, 
        /// sends the request message to the <paramref name="serverEndpoint"/>.
        /// 
        /// <para>
        /// If the request is not completed after <see cref="REQUEST_TIMEOUT_IN_MS"/>, it will be canceled and discarded.
        /// </para>
        /// </summary>
        /// <param name="serverEndpoint">The endpoint to send the request to.</param>
        /// <param name="request">The actual request to send.</param>
        /// <param name="cancellationToken">A cancellation token used to cancel the sending process.</param>
        /// <returns>Whether the request was successfully sent.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<bool> SendRequestInternalAsync(IPEndPoint serverEndpoint, Request request, CancellationToken cancellationToken)
        {
            if (_gameServerCommunication is null)
            {
                throw new InvalidOperationException("Communication is not started.");
            }

            AddToQueueCancelPreviousRequest(serverEndpoint, request);

            // register request cancellation
            // (does not need to be disposed since this lives as long as the request. May be moved into the request)
            _ = request.CancellationToken.Register(() =>
            {
                // remove the request from the queue
                if (_queuedRequests.TryGetValue(serverEndpoint, out var list))
                {
                    list.TryRemove(request);

                    if (list.Count == 0)
                    {
                        // remove the queue for this endpoint if empty
                        _queuedRequests.TryRemove(serverEndpoint, out _);
                    }
                }
            });

            try
            {
                // set timestamp just before sending
                request.Timestamp = DateTimeOffset.Now;

                // send request message
                await _gameServerCommunication.SendAsync(serverEndpoint, request.Message, cancellationToken: cancellationToken);

                // activate request to start timeout
                request.Activate();

                return true;
            }
            catch
            {
                // failed to send message (maybe server is not online)
                request.TryCancel();
                return false;
            }
        }

        /// <summary>
        /// Adds the request to the queued requests for the <paramref name="endpoint"/> and
        /// cancels + removes all previous requests with the same message.
        /// </summary>
        private void AddToQueueCancelPreviousRequest(IPEndPoint endpoint, Request request)
        {
            ConcurrentHashSet<Request> requests = _queuedRequests.GetOrAdd(endpoint, []);

            bool filter(Request x) =>
                x.Message.CommandName == request.Message.CommandName &&
                x.Message.Data == request.Message.Data &&
                x.Message.Separator == request.Message.Separator;

            // cancel and remove previous operations
            foreach (Request r in requests.Where(filter).ToList())
            {
                requests.TryRemove(r);
                r.ResponseCompletionSource?.TrySetCanceled();
                r.Dispose();
            }

            requests.Add(request);
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