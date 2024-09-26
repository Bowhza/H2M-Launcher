using H2MLauncher.Core.Matchmaking.Models;

namespace H2MLauncher.Core
{
    public interface IMatchmakingClient
    {
        //Task OnMatchmakingEntered();
        Task OnSearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults);
        Task OnMatchFound(string hostName, SearchMatchResult matchResult);
        Task OnRemovedFromMatchmaking(MatchmakingError reason);
    }
}
