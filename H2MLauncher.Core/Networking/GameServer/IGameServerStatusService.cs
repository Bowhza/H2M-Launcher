using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer;

namespace H2MLauncher.Core.Services
{
    public interface IGameServerStatusService<TServer> where TServer : IServerConnectionDetails
    {
        Task<GameServerStatus?> GetStatusAsync(TServer server, CancellationToken cancellationToken);

        Task<IAsyncEnumerable<(TServer server, GameServerStatus? status)>> GetStatusAsync(
            IEnumerable<TServer> servers,
            bool sendSynchronously = false,
            int requestTimeoutInMs = 10000,
            CancellationToken cancellationToken = default);

        Task<Task> SendGetStatusAsync(
            IEnumerable<TServer> servers,
            Action<ServerInfoEventArgs<TServer, GameServerStatus>> onStatusResponse,
            int timeoutInMs = 10000,
            CancellationToken cancellationToken = default);
    }
}