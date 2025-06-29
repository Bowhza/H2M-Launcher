﻿using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Party;

public interface IPartyHub
{
    Task<PartyInfo?> CreateParty();

    Task<PartyInfo?> JoinParty(string partyId);

    Task<bool> LeaveParty();

    Task JoinServer(SimpleServerInfo server);

    Task UpdatePlayerName(string newName);

    Task<bool> KickPlayer(string id);

    Task<bool> PromoteLeader(string id);

    Task<bool> ChangePartyPrivacy(PartyPrivacy newPartyPrivacy);

    Task<InviteInfo?> CreateInvite(string playerId);
}
