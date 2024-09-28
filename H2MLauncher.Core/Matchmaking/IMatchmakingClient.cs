using H2MLauncher.Core.Matchmaking.Models;

namespace H2MLauncher.Core
{
    public interface IMatchmakingClient
    {
        Task OnMatchmakingEntered(MatchmakingMetadata metadata);
        Task OnSearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults);
        Task OnMatchFound(string hostName, SearchMatchResult matchResult);
        Task OnRemovedFromMatchmaking(MatchmakingError reason);
    }
}
