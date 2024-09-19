using H2MLauncher.Core.Models;

namespace MatchmakingServer.SignalR;

public interface IPartyHub
{
    Task<string?> CreateParty();

    Task<IReadOnlyList<string>?> JoinParty(string partyId);

    Task<bool> LeaveParty();

    Task JoinServer(ServerConnectionDetails server);

    Task UpdatePlayerName(string newName);
}
