using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

namespace MatchmakingServer.SignalR
{
    public interface IClient
    {
        Task<bool> NotifyJoin(JoinServerInfo serverInfo, CancellationToken cancellationToken);

        Task QueuePositionChanged(int queuePosition, int queueSize);

        Task RemovedFromQueue(DequeueReason reason);

        Task SearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults);
        Task MatchFound(string hostName, SearchMatchResult matchResult);
        Task RemovedFromMatchmaking(MatchmakingError reason);
    }
}
