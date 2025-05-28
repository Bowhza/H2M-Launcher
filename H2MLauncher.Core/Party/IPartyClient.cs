using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

namespace MatchmakingServer.Core.Party;

public interface IPartyClient
{
    Task OnUserJoinedParty(string id, string playerName);
    Task OnUserLeftParty(string id);
    Task OnUserNameChanged(string id, string newPlayerName);
    Task OnPartyClosed();
    Task OnKickedFromParty();

    Task OnLeaderChanged(string oldLeaderId, string newLeaderId);

    Task OnPartyPrivacyChanged(PartyPrivacy oldPrivacy, PartyPrivacy newPrivacy);

    /// <summary>
    /// Called when the party leader joined a server.
    /// </summary>
    Task OnServerChanged(SimpleServerInfo server);

    Task OnConnectionRejected(string reason);
}
