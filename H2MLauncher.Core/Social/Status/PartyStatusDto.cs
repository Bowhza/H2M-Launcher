namespace H2MLauncher.Core.Social.Status;

public record PartyStatusDto(string PartyId, int Size, bool IsOpen, List<string> Invites);
