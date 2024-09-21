using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Joining;

public readonly record struct ServerJoinResult
{
    public required IServerConnectionDetails Server { get; init; }

    public JoinServerResult ResultCode { get; init; }

    public string? Password { get; init; }
}
