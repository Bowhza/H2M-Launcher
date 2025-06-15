namespace H2MLauncher.Core.Social;

public record PartyStatusDto(string PartyId, int Size, bool IsOpen, List<string> Invites);
