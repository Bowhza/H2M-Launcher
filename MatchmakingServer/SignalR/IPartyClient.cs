using H2MLauncher.Core.Models;

namespace MatchmakingServer.SignalR;

public interface IPartyClient
{
    Task OnUserJoinedParty(string id, string playerName);
    Task OnUserLeftParty(string id, string playerName);
    Task OnUserNameChanged(string id, string newPlayerName);
    Task OnPartyClosed();
    Task OnKickedFromParty();

    /// <summary>
    /// Called when the party joins a server.
    /// </summary>
    Task OnJoinServer(ServerConnectionDetails server);

    Task OnConnectionRejected(string reason);
}
