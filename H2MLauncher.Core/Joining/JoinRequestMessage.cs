﻿using CommunityToolkit.Mvvm.Messaging.Messages;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Joining;

public class JoinRequestMessage(ISimpleServerInfo server, string? password, JoinKind kind) : AsyncRequestMessage<JoinServerResult>
{
    public ISimpleServerInfo Server { get; } = server;

    public string? Password { get; } = password;

    public JoinKind Kind { get; } = kind;
}
