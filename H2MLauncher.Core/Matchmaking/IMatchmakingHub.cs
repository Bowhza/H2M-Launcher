using H2MLauncher.Core.Matchmaking.Models;

namespace H2MLauncher.Core
{
    public interface IMatchmakingHub
    {
        Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId);

        Task JoinAck(bool successful);

        Task LeaveQueue();

        Task<bool> SearchMatch(MatchSearchCriteria searchPreferences, List<string> preferredServers);

        Task<bool> UpdateSearchSession(MatchSearchCriteria searchPreferences, List<ServerPing> serverPings);
    }
}