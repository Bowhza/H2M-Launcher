using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer;

namespace H2MLauncher.Core.Services
{
    public interface ICanGetGameServerInfo<TServer> where TServer : IServerConnectionDetails
    {
        IAsyncEnumerable<(TServer server, GameServerInfo? info)> GetAllInfoAsync(IEnumerable<TServer> servers, int requestTimeoutInMs = 10000, CancellationToken cancellationToken = default);
        Task<IAsyncEnumerable<(TServer server, GameServerInfo? info)>> GetInfoAsync(IEnumerable<TServer> servers, bool sendSynchronously = false, int requestTimeoutInMs = 10000, CancellationToken cancellationToken = default);
        Task GetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse, int timeoutInMs = 10000, CancellationToken cancellationToken = default);
        Task<GameServerInfo?> GetInfoAsync(TServer server, CancellationToken cancellationToken);
        Task<IDisposable> SendGetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse, CancellationToken cancellationToken = default);
        Task<Task> SendGetInfoAsync(IEnumerable<TServer> servers, Action<ServerInfoEventArgs<TServer, GameServerInfo>> onInfoResponse, int timeoutInMs = 10000, CancellationToken cancellationToken = default);
    }
}