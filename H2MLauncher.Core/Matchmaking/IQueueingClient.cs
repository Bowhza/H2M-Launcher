using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

namespace H2MLauncher.Core
{
    public interface IQueueingClient
    {
        Task<bool> NotifyJoin(JoinServerInfo serverInfo, CancellationToken cancellationToken);

        Task OnQueuePositionChanged(int queuePosition, int queueSize);

        Task OnRemovedFromQueue(DequeueReason reason);
    }
}
