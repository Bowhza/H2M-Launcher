using H2MLauncher.Core.Models;

namespace MatchmakingServer.SignalR;

public interface IPartyClient
{
    void OnUserJoinedParty(string playerName);
    void OnUserLeftParty(string playerName);
    void OnPartyClosed();
    void OnKickedFromParty();

    Task OnJoinServer(ServerConnectionDetails server);

    Task OnConnectionRejected(string reason);
}
