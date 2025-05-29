namespace MatchmakingServer.Core.Social;

public record PartyStatusDto(string PartyId, int Size, bool IsOpen, List<string> Invites);