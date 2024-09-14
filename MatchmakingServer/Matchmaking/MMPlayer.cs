using H2MLauncher.Core.Models;

namespace MatchmakingServer
{
    internal sealed class MMPlayer
    {
        public Player Player { get; }
        public Dictionary<ServerConnectionDetails, int> PreferredServers { get; set; } // List of queued servers with ping

        public MatchSearchCriteria SearchPreferences { get; set; }

        public DateTime JoinTime { get; set; } // Time the player joined the queue
        public int SearchAttempts { get; set; } // Number of search attempts

        public List<MMMatch> PossibleMatches { get; init; } // Currently possible non eligible matches

        public MMPlayer(Player player, Dictionary<ServerConnectionDetails, int> servers, MatchSearchCriteria searchPreferences)
        {
            Player = player;
            PreferredServers = servers;
            SearchPreferences = searchPreferences;
            JoinTime = DateTime.Now; // Record the time they joined the queue
            PossibleMatches = new(servers.Count);
        }

        public bool IsEligibleForServer(GameServer server, int numPlayersForServer)
        {
            if (!PreferredServers.TryGetValue((server.ServerIp, server.ServerPort), out int ping)
                || ping <= 0)
            {
                return false;
            }

            if (SearchAttempts == 0 && numPlayersForServer < SearchPreferences.MinPlayers)
            {
                // On the first search attempt, wait until enough players available to potentially create a fresh match
                return false;
            }

            if (SearchPreferences.MaxScore >= 0 &&
                server.LastStatusResponse is not null &&
                server.LastStatusResponse.TotalScore > SearchPreferences.MaxScore)
            {
                return false;
            }

            if (SearchPreferences.MaxPlayersOnServer >= 0 &&
                server.LastServerInfo is not null &&
                server.LastServerInfo.RealPlayerCount > SearchPreferences.MaxPlayersOnServer)
            {
                return false;
            }

            if (SearchPreferences.MaxPing > 0 && ping > 0)
            {
                return ping < SearchPreferences.MaxPing;
            }

            return true;
        }
    }
}
