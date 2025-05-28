namespace MatchmakingServer.Social;

public enum FriendshipError
{
    RequestToYourself,
    AlreadyFriends,
    AlreadyRejected,
    RequestPending,
    UserNotFound,
    NoRequestFound,
    UnknownError
}
