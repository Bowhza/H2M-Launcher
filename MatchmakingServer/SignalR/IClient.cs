using H2MLauncher.Core.Matchmaking.Models;

namespace MatchmakingServer.SignalR
{
    public interface IClient
    {
        Task<bool> NotifyJoin(string serverIp, int serverPort, CancellationToken cancellationToken);

        Task QueuePositionChanged(int queuePosition, int queueSize);

        Task RemovedFromQueue(DequeueReason reason);

        Task SearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults);
        Task MatchFound(string hostName, SearchMatchResult matchResult);
        Task RemovedFromMatchmaking(MatchmakingError reason);
    }
}
