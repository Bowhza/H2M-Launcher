

using H2MLauncher.Core.Services;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace MatchmakingServer.SignalR
{
    public interface IQueueingHub
    {
        Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId, string playerName);

        Task JoinAck(bool successful);

        Task LeaveQueue();

        bool SearchMatch(string playerName, int minPlayers, int maxPing, List<string> preferredServers);

        bool UpdateSearchSession(int minPlayers, int maxPing, List<(string Ip, int Port, uint Ping)> serverPings);
    }
}