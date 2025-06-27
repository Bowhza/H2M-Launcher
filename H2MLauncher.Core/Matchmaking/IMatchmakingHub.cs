using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

namespace H2MLauncher.Core
{
    public interface IMatchmakingHub
    {
        Task<bool> JoinQueue(JoinServerInfo serverInfo);

        Task JoinAck(bool successful);

        Task LeaveQueue();

        Task<bool> SearchMatch(MatchSearchCriteria searchPreferences, string playlistId);

        Task<bool> SearchMatchCustom(MatchSearchCriteria searchPreferences, CustomPlaylist customPlaylist);

        Task<bool> UpdateSearchSession(MatchSearchCriteria searchPreferences, List<ServerPing> serverPings);
    }
}