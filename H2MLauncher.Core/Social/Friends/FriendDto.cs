using H2MLauncher.Core.Social.Status;

namespace H2MLauncher.Core.Social.Friends;

public record FriendDto(
    string Id, 
    string UserName,
    string? PlayerName,
    OnlineStatus Status, 
    GameStatus GameStatus,
    PartyStatusDto? PartyStatus,
    MatchStatusDto? MatchStatus,
    DateTimeOffset FriendsSince);
