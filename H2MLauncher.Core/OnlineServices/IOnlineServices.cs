using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.OnlineServices.Authentication;

namespace H2MLauncher.Core.OnlineServices;

public interface IOnlineServices
{
    ClientContext ClientContext { get; }
    bool IsPartyHubConnected { get; }
    bool IsQueueingHubConnected { get; }
    PlayerState State { get; }

    event Action<PlayerState, PlayerState>? StateChanged;
}
