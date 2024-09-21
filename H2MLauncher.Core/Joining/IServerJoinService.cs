using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Joining;

public interface IServerJoinService
{
    ISimpleServerInfo? LastServer { get; }

    event Action<ISimpleServerInfo>? ServerJoined;

    Task<JoinServerResult> JoinLastServer();
    Task<JoinServerResult> JoinServer(ISimpleServerInfo server, string? password);
    Task<JoinServerResult> JoinServer(IServerInfo serverInfo);
}
