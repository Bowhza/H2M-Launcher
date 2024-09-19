using H2MLauncher.Core.Matchmaking.Models;

namespace MatchmakingServer.SignalR
{
    public interface IQueueingHub
    {
        Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId);

        Task JoinAck(bool successful);

        Task LeaveQueue();

        bool SearchMatch(MatchSearchCriteria searchPreferences, List<string> preferredServers);

        bool UpdateSearchSession(MatchSearchCriteria searchPreferences, List<ServerPing> serverPings);
    }
}