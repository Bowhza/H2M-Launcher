namespace H2MLauncher.Core.Social;

public record FriendDto(
    string Id, 
    string UserName,
    string? PlayerName,
    OnlineStatus Status, 
    GameStatus GameStatus,
    PartyStatusDto? PartyStatus,
    DateTimeOffset FriendsSince);
