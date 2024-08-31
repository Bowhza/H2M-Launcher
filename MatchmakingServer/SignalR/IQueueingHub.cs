
namespace MatchmakingServer.SignalR
{
    public interface IQueueingHub
    {
        Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId, string playerName);

        Task JoinAck(string serverIp, int serverPort, bool successful);

        Task LeaveQueue();
    }
}