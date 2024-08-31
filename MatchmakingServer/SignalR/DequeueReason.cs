namespace MatchmakingServer.SignalR
{
    public enum DequeueReason
    {
        Unknown,
        UserLeave,
        MaxJoinAttemptsReached,
        JoinTimeout,
        Disconnect,
        Joined,
        JoinFailed
    }
}
