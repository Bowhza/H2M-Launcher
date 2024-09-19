namespace H2MLauncher.Core.Matchmaking.Models
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
