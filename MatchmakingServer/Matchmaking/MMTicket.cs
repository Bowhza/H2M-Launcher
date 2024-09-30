using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

namespace MatchmakingServer
{
    public sealed class MMTicket : IMMTicket
    {
        internal readonly object LockObj = new();
        public Guid Id { get; init; } = Guid.NewGuid();

        private readonly HashSet<Player> _players;
        public IReadOnlySet<Player> Players => _players;
        public Dictionary<ServerConnectionDetails, int> PreferredServers { get; set; }

        public MatchSearchCriteria SearchPreferences { get; set; }

        public DateTime JoinTime { get; set; }
        public int SearchAttempts { get; set; }

        public List<MMMatch> PossibleMatches { get; init; }

        public TaskCompletionSource<MMMatch> MatchCompletion { get; } = new();

        IReadOnlyList<MMMatch> IMMTicket.PossibleMatches => PossibleMatches.AsReadOnly();

        IReadOnlyCollection<ServerConnectionDetails> IMMTicket.PreferredServers => PreferredServers.Keys;

        bool IMMTicket.IsComplete => MatchCompletion.Task.IsCompleted;

        public MMTicket(IEnumerable<Player> players, Dictionary<ServerConnectionDetails, int> servers, MatchSearchCriteria searchPreferences)
        {
            _players = players.ToHashSet();
            PreferredServers = servers;
            SearchPreferences = searchPreferences;
            JoinTime = DateTime.Now; // Record the time they joined the queue
            PossibleMatches = new(servers.Count);
        }

        public bool RemovePlayer(Player player)
        {
            return _players.Remove(player);
        }

        public EligibilityResult IsEligibleForServer(GameServer server, int numPlayersForServer)
        {
            if (!PreferredServers.TryGetValue((server.ServerIp, server.ServerPort), out int ping)
                || ping <= 0)
            {
                return new(false, "No ping");
            }

            if (SearchAttempts == 0 && numPlayersForServer < SearchPreferences.MinPlayers)
            {
                // On the first search attempt, wait until enough players available to potentially create a fresh match
                return new(false, "Not enough players in queue (first attempt)");
            }

            if (SearchPreferences.MaxScore >= 0 &&
                server.LastStatusResponse is not null &&
                server.LastStatusResponse.TotalScore > SearchPreferences.MaxScore)
            {
                return new(false, "Score too high");
            }

            if (SearchPreferences.MaxPlayersOnServer >= 0 &&
                server.LastServerInfo is not null &&
                server.LastServerInfo.RealPlayerCount > SearchPreferences.MaxPlayersOnServer)
            {
                return new(false, "Max players on server");
            }

            if (SearchPreferences.MaxPing > 0 && ping > SearchPreferences.MaxPing)
            {
                return new(false, "Max ping");
            }

            return new(true, null);
        }

        public override string ToString()
        {
            return $"[{string.Join(',', Players.Select(p => p.Name))}]";
        }

        Task<MMMatch> IMMTicket.WaitForMatchAsync()
        {
            return MatchCompletion.Task;
        }
    }
}
