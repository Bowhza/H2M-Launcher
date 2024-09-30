using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Joining;

public interface IServerJoinService
{
    ISimpleServerInfo? LastServer { get; }
    bool IsJoining { get; }

    event Action<ISimpleServerInfo, JoinKind>? ServerJoined;

    Task<JoinServerResult> JoinLastServer();
    Task<JoinServerResult> JoinServer(ISimpleServerInfo server, string? password, JoinKind kind);
    Task<JoinServerResult> JoinServer(IServerInfo serverInfo, JoinKind kind);
}
