namespace H2MLauncher.Core.Party;

public record PartyInfo(string PartyId, PartyPrivacy PartyPrivacy, List<PartyPlayerInfo> Members, List<InviteInfo> Invites);

public record InviteInfo(string PlayerId, DateTime ExpirationTime);
