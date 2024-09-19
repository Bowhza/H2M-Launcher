using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

namespace MatchmakingServer.SignalR;

public interface IPartyHub
{
    Task<string?> CreateParty();

    Task<IReadOnlyList<PartyPlayerInfo>?> JoinParty(string partyId);

    Task<bool> LeaveParty();

    Task JoinServer(ServerConnectionDetails server);

    Task UpdatePlayerName(string newName);
}
