namespace H2MLauncher.Core.Social;

public interface ISocialClient
{
    Task OnFriendOnline(string friendId, string playerName);

    Task OnFriendOffline(string friendId);

    Task OnFriendStatusChanged(string friendId, string playerName, GameStatus status, PartyStatusDto? partyStatus, MatchStatusDto? matchStatus);

    Task OnFriendRequestAccepted(FriendDto newFriend);

    Task OnFriendRequestReceived(FriendRequestDto request);

    Task OnUnfriended(string byFriendId);

    Task OnMatchStatusUpdated(MatchStatusDto? matchStatus);
}
