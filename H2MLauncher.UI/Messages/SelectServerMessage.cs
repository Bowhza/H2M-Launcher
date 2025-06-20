using CommunityToolkit.Mvvm.Messaging.Messages;

using H2MLauncher.Core.Models;

namespace H2MLauncher.UI.Messages;

public sealed class SelectServerMessage(IServerConnectionDetails selectedServer)
    : ValueChangedMessage<IServerConnectionDetails>(selectedServer);
