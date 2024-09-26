using System.Runtime.CompilerServices;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer;

using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Services
{
    public sealed class TcpUdpDynamicGameServerInfoService<TServer> : IGameServerInfoService<TServer> where TServer : IServerConnectionDetails
    {
        private readonly IMasterServerService _hmwMasterServerService;
        private readonly IGameServerInfoService<TServer> _tcpGameServerInfoService;
        private readonly IGameServerInfoService<TServer> _udpGameServerInfoService;

        public TcpUdpDynamicGameServerInfoService(
            [FromKeyedServices("HMW")] IMasterServerService hmwMasterServerService,
            [FromKeyedServices("TCP")] IGameServerInfoService<TServer> tcpGameServerInfoService,
            [FromKeyedServices("UDP")] IGameServerInfoService<TServer> udpGameServerInfoService)
        {
            _hmwMasterServerService = hmwMasterServerService;
            _tcpGameServerInfoService = tcpGameServerInfoService;
            _udpGameServerInfoService = udpGameServerInfoService;
        }
        public async IAsyncEnumerable<(TServer server, GameServerInfo? info)> GetAllInfoAsync(
            IEnumerable<TServer> servers, 
            int requestTimeoutInMs = 10000, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            (IEnumerable<TServer> tcpServers, IEnumerable<TServer> udpServers) = await SplitServers(servers, cancellationToken);

            var udpResponses = _udpGameServerInfoService.GetAllInfoAsync(tcpServers, requestTimeoutInMs, cancellationToken);
            var tcpResponses = _tcpGameServerInfoService.GetAllInfoAsync(udpServers, requestTimeoutInMs, cancellationToken);

            var mergedResponses = AsyncEnumerableEx.Merge(udpResponses, tcpResponses);

            await foreach ((TServer server, GameServerInfo? info) in mergedResponses.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                yield return (server, info);
            }
        }

        public async Task<GameServerInfo?> GetInfoAsync(TServer server, CancellationToken cancellationToken)
        {
            IReadOnlySet<ServerConnectionDetails> hmwServers = await _hmwMasterServerService.GetServersAsync(cancellationToken);

            if (IsTcpServer(hmwServers, server))
            {
                return await _tcpGameServerInfoService.GetInfoAsync(server, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await _udpGameServerInfoService.GetInfoAsync(server, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<IAsyncEnumerable<(TServer server, GameServerInfo? info)>> GetInfoAsync(
            IEnumerable<TServer> servers,
            bool sendSynchronously = false,
            int requestTimeoutInMs = 10000,
            CancellationToken cancellationToken = default)
        {
            (IEnumerable<TServer> tcpServers, IEnumerable<TServer> udpServers) = await SplitServers(servers, cancellationToken);

            var udpResponses = await _udpGameServerInfoService.GetInfoAsync(tcpServers, sendSynchronously, requestTimeoutInMs, cancellationToken);
            var tcpResponses = await _tcpGameServerInfoService.GetInfoAsync(udpServers, sendSynchronously, requestTimeoutInMs, cancellationToken);

            return AsyncEnumerableEx.Merge(udpResponses, tcpResponses);
        }

        public async Task<Task> SendGetInfoAsync(
            IEnumerable<TServer> servers, 
            Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse, 
            int timeoutInMs = 10000, 
            CancellationToken cancellationToken = default)
        {
            (IEnumerable<TServer> tcpServers, IEnumerable<TServer> udpServers) = await SplitServers(servers, cancellationToken);

            Task udpResponsesTask = await _udpGameServerInfoService.SendGetInfoAsync(tcpServers, onInfoResponse, timeoutInMs, cancellationToken);
            Task tcpResponsesTask = await _tcpGameServerInfoService.SendGetInfoAsync(udpServers, onInfoResponse, timeoutInMs, cancellationToken);

            return Task.WhenAll(udpResponsesTask, tcpResponsesTask);
        }

        private static bool IsTcpServer(IReadOnlySet<ServerConnectionDetails> hmwServers, TServer server)
        {
            if (server is ServerConnectionDetails serverConnectionDetails)
            {
                return hmwServers.Contains(serverConnectionDetails);
            }
            else
            {
                return hmwServers.Contains((server.Ip, server.Port));
            }
        }

        public async Task<(IEnumerable<TServer> tcpServers, IEnumerable<TServer> udpServers)> SplitServers(
            IEnumerable<TServer> servers,
            CancellationToken cancellationToken)
        {
            IReadOnlySet<ServerConnectionDetails> hmwServers = await _hmwMasterServerService.GetServersAsync(cancellationToken);
            IEnumerable<TServer> tcpServers = servers.Where(s => IsTcpServer(hmwServers, s));
            IEnumerable<TServer> udpServers = servers.Where(s => !IsTcpServer(hmwServers, s));

            return (tcpServers, udpServers);
        }
    }
}