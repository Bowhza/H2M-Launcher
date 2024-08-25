namespace MatchmakingServer.SignalR
{
    public interface IClient
    {
        Task<bool> NotifyJoin(string serverIp, int serverPort);

        Task QueuePositionChanged(int queuePosition, int queueSize);
    }
}
