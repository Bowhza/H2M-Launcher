namespace MatchmakingServer.SignalR
{
    public interface IClient
    {
        Task<bool> NotifyJoinAsync(string serverIp, int serverPort);
    }
}
