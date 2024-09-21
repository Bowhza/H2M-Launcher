using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Joining;

public interface IServerJoinService
{
    IServerConnectionDetails? LastServer { get; }

    event Action<IServerConnectionDetails>? ServerJoined;

    Task<bool> JoinLastServer();
    Task<bool> JoinServer(IServerConnectionDetails server, string? password);
    Task<ServerJoinResult> JoinServer(IServerInfo serverInfo);
}
