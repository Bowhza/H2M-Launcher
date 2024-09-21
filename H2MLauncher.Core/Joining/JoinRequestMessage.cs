using CommunityToolkit.Mvvm.Messaging.Messages;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Joining;

public class JoinRequestMessage(IServerConnectionDetails server, string? password) : AsyncRequestMessage<bool>
{
    public IServerConnectionDetails Server { get; } = server;

    public string? Password { get; } = password;
}
