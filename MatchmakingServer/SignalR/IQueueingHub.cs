using H2MLauncher.Core.Models;

namespace MatchmakingServer.SignalR
{
    public interface IQueueingHub
    {
        Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId, string playerName);

        Task JoinAck(bool successful);

        Task LeaveQueue();

        bool SearchMatch(string playerName, MatchSearchCriteria searchPreferences, List<string> preferredServers);

        bool UpdateSearchSession(MatchSearchCriteria searchPreferences, List<ServerPing> serverPings);
    }
}