using System.Collections.Concurrent;
using System.Net;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Networking.GameServer.HMW
{
    public sealed class HttpGameServerInfoService<TServer> : IGameServerInfoService<TServer> where TServer : IServerConnectionDetails
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpGameServerInfoService<TServer>> _logger;
        private const int MAX_PARALLEL_REQUESTS = 15;

        public HttpGameServerInfoService(ILogger<HttpGameServerInfoService<TServer>> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async IAsyncEnumerable<(TServer server, GameServerInfo? info)> GetAllInfoAsync(
            IEnumerable<TServer> servers,
            int requestTimeoutInMs = 10000,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IAsyncEnumerable<(TServer server, GameServerInfo? info)> results = await GetInfoAsync(
                servers, sendSynchronously: true, requestTimeoutInMs, cancellationToken);

            await foreach ((TServer server, GameServerInfo? info) in results.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                yield return (server, info);
            }
        }

        public async Task<IAsyncEnumerable<(TServer server, GameServerInfo? info)>> GetInfoAsync(
            IEnumerable<TServer> servers,
            bool sendSynchronously = false,
            int requestTimeoutInMs = 10000,
            CancellationToken cancellationToken = default)
        {
            Channel<(TServer server, GameServerInfo? info)> channel = Channel.CreateUnbounded<(TServer, GameServerInfo?)>();
            ConcurrentBag<Task> continuations = [];

            Task requestTask = Parallel.ForEachAsync(
                servers,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = MAX_PARALLEL_REQUESTS,
                    CancellationToken = cancellationToken
                },
                async (server, ct) =>
                {
                    CancellationTokenSource timeoutCancellation = new(requestTimeoutInMs);
                    CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token, ct);
                    try
                    {
                        var task = await GetInfoCoreAsync(server, linkedCancellation.Token);
                        continuations.Add(task.ContinueWith(
                                    t =>
                                    {
                                        if (t.IsCompletedSuccessfully)
                                        {
                                            channel.Writer.TryWrite((server, t.Result));
                                        }
                                    },
                                    CancellationToken.None,
                                    TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default));
                    }
                    catch (Exception ex)
                    {
                        // Propagate the error by completing the writer with it
                        channel.Writer.TryComplete(ex);
                    }
                    finally
                    {
                        timeoutCancellation.Dispose();
                        linkedCancellation.Dispose();
                    }
                }).ContinueWith((task) =>
                {
                    return Task.WhenAll(continuations)
                               .ContinueWith(t => channel.Writer.TryComplete(t.Exception));
                });

            if (sendSynchronously)
            {
                await requestTask;
            }

            return channel.Reader.ReadAllAsync(CancellationToken.None);
        }

        public async Task GetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            int timeoutInMs = 10000, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellation = new(timeoutInMs);
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token, cancellationToken);
            try
            {
                await Parallel.ForEachAsync(
                    servers,
                    new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = MAX_PARALLEL_REQUESTS,
                        CancellationToken = linkedCancellation.Token
                    },
                    async (server, ct) =>
                    {

                        try
                        {
                            GameServerInfo? info = await GetInfoAsync(server, linkedCancellation.Token);
                            if (info is not null)
                            {
                                onInfoResponse(new() { Server = server, ServerInfo = info });
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // canceled
                        }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // canceled
            }
            finally
            {
                timeoutCancellation.Dispose();
                linkedCancellation.Dispose();
            }
        }

        public Task<GameServerInfo?> GetInfoAsync(TServer server, CancellationToken cancellationToken)
        {
            return GetInfoCoreAsync(server, cancellationToken).Unwrap();
        }

        public async Task<Task> SendGetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            int timeoutInMs = 10000, CancellationToken cancellationToken = default)
        {
            (Task task, IDisposable disposable) = await SendGetInfoWithCallbackAsync(
                servers, onInfoResponse, timeoutInMs, cancellationToken).ConfigureAwait(false);

            return task;
        }

        public async Task<IDisposable> SendGetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            CancellationToken cancellationToken = default)
        {
            (Task task, IDisposable disposable) = await SendGetInfoWithCallbackAsync(
                servers, onInfoResponse, cancellationToken: cancellationToken).ConfigureAwait(false);

            return disposable;
        }

        private async Task<(Task, IDisposable)> SendGetInfoWithCallbackAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse,
            int timeoutInMs = 10000, CancellationToken cancellationToken = default)
        {
            IAsyncEnumerable<(TServer server, GameServerInfo? info)> responses = await GetInfoAsync(
                servers, sendSynchronously: true, timeoutInMs, cancellationToken);

            var timeoutCancellation = new CancellationTokenSource(timeoutInMs);
            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token, cancellationToken);
            IDisposable disposable = Disposable.Create(linkedCancellation.Cancel);

            Task responseTask = Task.Run(async () =>
            {
                try
                {
                    await foreach ((TServer server, GameServerInfo? info) response in responses.ConfigureAwait(false).WithCancellation(linkedCancellation.Token))
                    {
                        if (response.info is not null)
                        {
                            onInfoResponse(new() { Server = response.server, ServerInfo = response.info });
                        }
                    }
                }
                catch { }
                finally
                {
                    timeoutCancellation.Dispose();
                    linkedCancellation.Dispose();
                }
            }, CancellationToken.None);

            return (responseTask, disposable);
        }

        private static async Task<GameServerInfo?> ReadInfoFromResponseAsync(HttpResponseMessage response, TServer server, HttpEventListener.HttpRequestTimings timings,
          CancellationToken cancellationToken)
        {
            HMWGameServerInfo? info = await response.Content.TryReadFromJsonAsync<HMWGameServerInfo>(cancellationToken);
            if (info is null)
            {
                return null;
            }

            int ping = (int)(timings.Response?.TotalMilliseconds ?? timings.Request?.TotalMilliseconds ?? -1);

            if (!IPAddress.TryParse(server.Ip, out IPAddress? address))
            {
                return null;
            }

            return new GameServerInfo()
            {
                Address = new(address, server.Port),
                Clients = info.Clients,
                Bots = info.Bots,
                MaxClients = info.MaxClients,
                IsPrivate = info.IsPrivate == 1,
                PrivilegedSlots = info.PrivateClients,
                ModName = "HMW",
                HostName = info.HostName,
                GameName = info.Game,
                GameType = info.GameType,
                MapName = info.MapName,
                PlayMode = info.PlayMode,
                Protocol = info.Protocol,
                Players = info.Players.Select(p => new GamePlayerStatus(0, p.Ping, p.Name)).ToArray(),
                Ping = ping,
            };
        }


        private async Task<Task<GameServerInfo?>> GetInfoCoreAsync(TServer server, CancellationToken cancellationToken)
        {
            try
            {
                UriBuilder uriBuilder = new()
                {
                    Scheme = "http",
                    Host = server.Ip,
                    Port = server.Port,
                    Path = "getInfo"
                };

                Uri url = uriBuilder.Uri;

                HttpResponseMessage response;
                HttpEventListener.HttpRequestTimings timings;

                using (HttpEventListener listener = new())
                {
                    response = await _httpClient.GetAsync(url, cancellationToken);
                    timings = listener.GetTimings();
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Task.FromResult<GameServerInfo?>(null);
                }

                return ReadInfoFromResponseAsync(response, server, timings, cancellationToken);
            }
            catch (OperationCanceledException) { return Task.FromResult<GameServerInfo?>(null); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while requesting server info from {server}", server);

                return Task.FromResult<GameServerInfo?>(null);
            }
        }


    }
}
