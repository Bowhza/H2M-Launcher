using System.Collections.Concurrent;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;

using Nito.Disposables.Internals;

namespace MatchmakingServer
{
    public class MatchmakingService
    {
        /// <summary>
        /// The time in queue after which a fresh lobby is not exclusively created and 
        /// joined players are also calculated in the min players threshold.
        /// </summary>
        private const int FRESH_LOBBY_TIMEOUT_SECONDS = 20;
        private const int MATCHMAKING_INTERVAL_MS = 3000;

        private readonly ConcurrentDictionary<(string ip, int port), ConcurrentLinkedQueue<MMPlayer>> _serverGroups = [];
        private readonly ConcurrentLinkedQueue<MMPlayer> _playerQueue = new();

        private readonly ServerStore _serverStore;
        private readonly IHubContext<QueueingHub, IClient> _hubContext;
        private readonly QueueingService _queueingService;
        private readonly GameServerCommunicationService<GameServer> _gameServerCommunicationService;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(
            ServerStore serverStore,
            IHubContext<QueueingHub, IClient> hubContext,
            QueueingService queueingService,
            GameServerCommunicationService<GameServer> gameServerCommunicationService,
            ILogger<MatchmakingService> logger)
        {
            _serverStore = serverStore;
            _hubContext = hubContext;
            _queueingService = queueingService;
            _gameServerCommunicationService = gameServerCommunicationService;
            _logger = logger;

            Task.Run(async () =>
            {
                while (true)
                {
                    while (_playerQueue.Count == 0)
                    {
                        // wait for players to enter matchmaking
                        await Task.Delay(500);
                    }

                    await CheckForMatches();
                    await Task.Delay(MATCHMAKING_INTERVAL_MS);
                }
            });
        }

        internal record struct MMMatch(GameServer Server, double MatchQuality, List<MMPlayer> SelectedPlayers)
        {
            public static implicit operator (GameServer server, double matchQuality, List<MMPlayer> selectedPlayers)(MMMatch value)
            {
                return (value.Server, value.MatchQuality, value.SelectedPlayers);
            }

            public static implicit operator MMMatch((GameServer server, double matchQuality, List<MMPlayer> selectedPlayers) value)
            {
                return new MMMatch(value.server, value.matchQuality, value.selectedPlayers);
            }
        }

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

        public bool EnterMatchmaking(Player player, MatchSearchCriteria searchPreferences, List<string> preferredServers)
        {
            if (player.State is not (PlayerState.Connected or PlayerState.Joined))
            {
                // invalid player state
                _logger.LogDebug("Cannot enter matchmaking: invalid state {player}", player);
                return false;
            }

            _logger.LogDebug("Entering matchmaking for player {player} (searchPreferences: {@searchPreferences}, servers: {numPreferredServers})",
                player, searchPreferences, preferredServers.Count);

            player.State = PlayerState.Matchmaking;

            List<ServerConnectionDetails> preferredServersParsed = [];

            foreach (var address in preferredServers)
            {
                string[] splitted = address.Split(':');
                if (splitted.Length != 2)
                {
                    continue;
                }

                string ip = splitted[0];
                if (!int.TryParse(splitted[1], out int port))
                {
                    continue;
                }

                var key = (ip, port);
                preferredServersParsed.Add(key);

                // Make sure server is created and running queue
                GameServer server = _serverStore.GetOrAddServer(ip, port, "");
                _queueingService.StartQueue(server);
            }

            AddPlayerToQueue(new MMPlayer(player, preferredServersParsed.ToDictionary(x => x, _ => -1), searchPreferences));

            return true;
        }

        public bool LeaveMatchmaking(Player player)
        {
            if (player.State is not PlayerState.Matchmaking)
            {
                // invalid player state
                _logger.LogDebug("Cannot leave matchmaking: invalid state {player}", player);
                return false;
            }

            MMPlayer? queuedPlayer = _playerQueue.FirstOrDefault(p => p.Player == player);
            if (queuedPlayer is null)
            {
                _logger.LogWarning("Player {player} not queued in Matchmaking despite state. Correcting state to 'Connected'.", player);
                player.State = PlayerState.Connected;
                return false;
            }

            RemovePlayer(queuedPlayer);
            player.State = PlayerState.Connected;

            _logger.LogInformation("Player {player} removed from matchmaking", player);
            return true;
        }

        private void AddPlayerToQueue(MMPlayer player)
        {
            _playerQueue.Enqueue(player);

            foreach (ServerConnectionDetails server in player.PreferredServers.Keys)
            {
                if (!_serverGroups.ContainsKey(server))
                {
                    _serverGroups[server] = [];
                }

                _serverGroups[server].Enqueue(player);
            }

            _logger.LogDebug("Player {mmPlayer} entered matchmaking", player);
        }

        public bool UpdateSearchPreferences(Player player, MatchSearchCriteria matchSearchPreferences, List<ServerPing> serverPings)
        {
            if (player.State is not PlayerState.Matchmaking)
            {
                // invalid player state
                _logger.LogDebug("Cannot update search session: invalid state {player}", player);
                return false;
            }

            MMPlayer? queuedPlayer = _playerQueue.FirstOrDefault(p => p.Player == player);
            if (queuedPlayer is null)
            {
                _logger.LogWarning("Player {player} not queued in Matchmaking despite state. Correcting state to 'Connected'.", player);
                player.State = PlayerState.Connected;
                return false;
            }

            _logger.LogTrace("Updating search preferences for {player}: {searchPreferences} ({numServerPings} server pings)",
                player, matchSearchPreferences, serverPings.Count);

            queuedPlayer.SearchPreferences = matchSearchPreferences;

            foreach ((string serverIp, int serverPort, uint ping) in serverPings)
            {
                queuedPlayer.PreferredServers[(serverIp, serverPort)] = Math.Min(999, (int)ping);
            }

            return true;
        }

        private static double CalculateServerQuality(GameServer server)
        {
            if (server?.LastServerInfo is null)
            {
                return 0; // Invalid server, assign lowest score
            }

            double baseQuality = 1000; // Start with a base score for every server

            // Check if the server is "half full" and under the score limit and probably needs players
            bool isEmpty = server.LastServerInfo.RealPlayerCount == 0;

            // Case 1: If server is empty, give it a high bonus
            if (isEmpty)
            {
                baseQuality += 1000;

                return baseQuality;
            }

            bool isHalfFull = server.LastStatusResponse?.TotalScore < 3000 && server.LastServerInfo.RealPlayerCount < 6;

            // Case 2: If server is under the score limit and half full, give it a significant bonus
            if (isHalfFull)
            {
                baseQuality += 3000;
            }

            double totalScoreAssumption = server.LastStatusResponse?.TotalScore ?? 10000; // assume average score

            // Calculate proportional penalty based on TotalScore (higher score means lower quality)
            double totalScorePenalty = Math.Min(totalScoreAssumption / 300, 600); // cut of at 20000 score

            // Apply proportional penalties for TotalScore and available slots
            baseQuality -= totalScorePenalty;   // Higher TotalScore reduces the quality

            return baseQuality;
        }

        class MmServerPriorityComparer : IComparer<GameServer>
        {
            public int Compare(GameServer? x, GameServer? y)
            {
                if (x?.LastServerInfo is null || y?.LastServerInfo is null)
                {
                    return 0;
                }

                // Check if we should prioritize based on TotalScore < 1000
                bool xIsHalfFull = x.LastStatusResponse?.TotalScore < 1000 && x.LastServerInfo.RealPlayerCount < 6;
                bool yIsHalfFull = y.LastStatusResponse?.TotalScore < 1000 && x.LastServerInfo.RealPlayerCount < 6;

                // Case 1: If both servers are half empty and under the score limit,
                // prioritize by player count (servers with fewer players should be prioritized)
                if (xIsHalfFull && yIsHalfFull)
                {
                    return x.LastServerInfo.RealPlayerCount.CompareTo(y.LastServerInfo.RealPlayerCount);
                }

                // Case 2: If one server is half full and under the score limit and the other one is not,
                // prioritize the half full server
                if (xIsHalfFull && !yIsHalfFull)
                {
                    return -1;
                }
                if (!xIsHalfFull && yIsHalfFull)
                {
                    return 1;
                }

                // Case 3: If both servers are over the score limit, prioritize by fewer players
                return x.LastServerInfo.RealPlayerCount.CompareTo(y.LastServerInfo.RealPlayerCount);
            }
        }

        public async Task CheckForMatches()
        {
            var respondingServers = new List<GameServer>(_serverGroups.Count);
            var serversToRequest = _serverGroups.Keys
                .Select(key => _serverStore.Servers.TryGetValue(key, out GameServer? server) ? server : null)
                .WhereNotNull();

            using CancellationTokenSource requestCancellation = new();

            _logger.LogTrace("Requesting server info...");

            try
            {
                // Request server info for all servers part of matchmaking rn
                Task getInfoCompleted = await _gameServerCommunicationService.SendGetInfoAsync(serversToRequest, (e) =>
                {
                    e.Server.LastServerInfo = e.ServerInfo;
                    e.Server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;

                    respondingServers.Add(e.Server);
                }, timeoutInMs: 2000, cancellationToken: requestCancellation.Token);

                // Immediately after send info requests send status requests
                Task getStatusCompleted = await _gameServerCommunicationService.SendGetStatusAsync(serversToRequest, (e) =>
                {
                    e.Server.LastStatusResponse = e.ServerInfo;
                }, timeoutInMs: 2000, cancellationToken: requestCancellation.Token);

                // Wait for all to complete / time out
                await Task.WhenAll(getInfoCompleted, getStatusCompleted);
            }
            catch (OperationCanceledException) when (!requestCancellation.IsCancellationRequested)
            {
                // expected timeout
            }

            _logger.LogDebug("Server info received from {numServers}. Sorting servers by match quality...", respondingServers.Count);

            // Sort servers: prioritize fresh servers with low player count
            //respondingServers.Sort(new MmServerPriorityComparer());

            var orderedServers = respondingServers.Select(s => (server: s, qualityScore: CalculateServerQuality(s)))
                .OrderByDescending(x => x.qualityScore)
                .ToList();

            _logger.LogDebug("Selecting players for matchmaking...");

            List<MMMatch> matches = [];
            foreach (MMPlayer player in _playerQueue)
            {
                player.PossibleMatches.Clear();
            }

            //Dictionary<MMPlayer, List<MMMatch>> theoreticalMatchesPerPlayer = _playerQueue
            //    .ToDictionary(p => p, p => new List<MMMatch>(p.PreferredServers.Count));

            do
            {
                // Iterate through prioritized servers
                foreach ((GameServer server, double qualityScore) in orderedServers)
                {
                    int availableSlots = Math.Max(0, server.LastServerInfo!.FreeSlots - server.UnavailableSlots);

                    _logger.LogTrace("Server {server} has {numPlayers} players, {numAvailableSlots} available slots, {totalScore} total score => Quality {qualityScore}",
                        server, server.LastServerInfo.RealPlayerCount, availableSlots, server.LastStatusResponse?.TotalScore, qualityScore);

                    if (availableSlots <= 0)
                        continue; // Skip if no free slots are available

                    // Sort players based on their min player threshold (ascending order) and check whether servers meets their criteria
                    List<(MMPlayer player, bool isEligible)> playersForServerSorted = GetPlayersForServer(server);
                    if (playersForServerSorted.Count == 0)
                    {
                        // no players
                        continue;
                    }

                    List<MMPlayer> eligiblePlayers = playersForServerSorted.Where(p => p.isEligible).Select(p => p.player).ToList();

                    _logger.LogTrace("{numPlayers} players ({numEligible} eligible) in matchmaking queue for server {server}",
                        playersForServerSorted.Count, eligiblePlayers.Count, server);

                    // find a valid match for all eligible players
                    if (TrySelectMatch(server, eligiblePlayers, qualityScore, availableSlots, out MMMatch validMatch))
                    {
                        matches.Add(validMatch);

                        _logger.LogTrace("Potential match found: {validMatch}",
                            new
                            {
                                NumPlayers = validMatch.SelectedPlayers.Count,
                                TotalPlayers = validMatch.SelectedPlayers.Count + server.LastServerInfo.RealPlayerCount,
                                AdjustedQuality = validMatch.MatchQuality
                            });
                    }

                    _logger.LogTrace("Finding best possible matches for non eligible players...");

                    // find overall best possible match for each non eligible player
                    foreach ((MMPlayer player, bool isEligible) in playersForServerSorted)
                    {
                        if (isEligible) continue;

                        bool foundMatch = TrySelectMatch(
                            server,
                            playersForServerSorted.Where(p => p.isEligible || p.player == player).Select(p => p.player).ToList(), // include only other eligible players
                            qualityScore,
                            availableSlots,
                            out MMMatch match);

                        if (!foundMatch)
                        {
                            continue;
                        }

                        player.PossibleMatches.Add(match);

                        _logger.LogTrace("Possible match found for player {player}: {validMatch}",
                            player.Player,
                            new
                            {
                                NumPlayers = match.SelectedPlayers.Count,
                                TotalPlayers = match.SelectedPlayers.Count + server.LastServerInfo.RealPlayerCount,
                                AdjustedQuality = match.MatchQuality
                            });
                    }
                }

                if (matches.Count == 0)
                {
                    // no more match
                    _logger.LogDebug("No more matches found");
                    break;
                }

                // Find match with best quality
                MMMatch bestMatch = matches.OrderByDescending(x => x.MatchQuality).First();

                _logger.LogDebug("Best match found: {bestMatch}", new
                {
                    bestMatch.Server,
                    NumPlayers = bestMatch.SelectedPlayers.Count,
                    TotalPlayers = bestMatch.SelectedPlayers.Count + bestMatch.Server.LastServerInfo!.RealPlayerCount,
                    AdjustedQuality = bestMatch.MatchQuality,
                });

                await CreateMatchAsync(bestMatch);

                // clear collections for next iteration
                matches.Clear();
                foreach (MMPlayer player in _playerQueue)
                {
                    player.PossibleMatches.Clear();
                }
            } while (true);


            // Notify players of theoretically possible matches
            List<Task> tasks = [];
            foreach (MMPlayer player in _playerQueue)
            {
                player.SearchAttempts++;
                tasks.Add(SendMatchSearchResults(player, player.PossibleMatches));
            }

            await Task.WhenAll(tasks);
        }

        private async Task SendMatchSearchResults(MMPlayer player, List<MMMatch> matchesForPlayer)
        {
            try
            {
                await _hubContext.Clients.Client(player.Player.ConnectionId)
                        .SearchMatchUpdate(matchesForPlayer.Select(CreateMatchResult))
                        .ConfigureAwait(false);
            }
            catch
            {
                _logger.LogWarning("Could not send match search results to player {player}", player.Player);
            }
        }

        private static SearchMatchResult CreateMatchResult(MMMatch match)
        {
            return new SearchMatchResult()
            {
                ServerIp = match.Server.ServerIp,
                ServerPort = match.Server.ServerPort,
                MatchQuality = match.MatchQuality,
                NumPlayers = match.SelectedPlayers.Count,
                ServerScore = match.Server.LastStatusResponse?.TotalScore
            };
        }

        private List<(MMPlayer player, bool isEligible)> GetPlayersForServer(GameServer server)
        {
            if (!_serverGroups.TryGetValue((server.ServerIp, server.ServerPort), out var playersForServer))
                return [];

            // Sort players based on their min player threshold (ascending order)
            // and check whether server meets their criteria
            return playersForServer
                .OrderBy(p => p.SearchPreferences.MinPlayers)
                .Select(player => (player, isEligible: player.IsEligibleForServer(server, playersForServer.Count)))
                .ToList();
        }

        internal bool TrySelectMatch(GameServer server, IReadOnlyList<MMPlayer> players, double serverQuality, int availableSlots, out MMMatch match)
        {
            List<MMPlayer> selectedPlayers = SelectMaxPlayersForMatch(
                players,
                server.LastServerInfo!.RealPlayerCount,
                availableSlots);

            if (selectedPlayers.Count > 0)
            {
                double adjustedQualityScore = AdjustedServerQuality(server, serverQuality, selectedPlayers);
                match = (server, adjustedQualityScore, selectedPlayers);

                return true;
            }

            match = default;
            return false;
        }

        internal double AdjustedServerQuality(GameServer server, double qualityScore, List<MMPlayer> potentialPlayers)
        {
            DateTime now = DateTime.Now;

            double avgWaitTime = potentialPlayers.Average(p => (now - p.JoinTime).TotalSeconds);
            double waitTimeFactor = 40;

            double avgMaxPing = potentialPlayers.Where(p => p.SearchPreferences.MaxPing > 0)
                .Average(p => p.SearchPreferences.MaxPing);
            List<double> pingDeviations = potentialPlayers
                .Select(p => p.PreferredServers[(server.ServerIp, server.ServerPort)])
                .Where(ping => ping >= 0)
                .Select(ping => ping - avgMaxPing)
                .ToList();

            double avgPingDeviation = pingDeviations.Count != 0 ? pingDeviations.Average() : 0;
            double pingFactor = 15;

            _logger.LogTrace("Adjusting quality based on avg wait time ({avgWaitTime} s) and ping deviation ({avgPingDeviation} ms)",
                Math.Round(avgWaitTime, 1), Math.Round(avgPingDeviation, 1));

            return qualityScore + (potentialPlayers.Count * 15) + (waitTimeFactor * avgWaitTime) - (pingFactor * avgPingDeviation);
        }

        internal static List<MMPlayer> SelectMaxPlayersForMatchWithTimeout(IEnumerable<MMPlayer> queuedPlayers, int joinedPlayersCount, int freeSlots)
        {
            DateTime now = DateTime.Now;

            // Sort players based on their min player threshold (ascending order)
            List<MMPlayer> playersForServer = queuedPlayers.OrderBy(p => p.SearchPreferences.MinPlayers).ToList();

            // Try to create the largest possible group of players
            List<MMPlayer> selectedPlayers = SelectMaxPlayersForMatch(playersForServer, 0, freeSlots);
            if (selectedPlayers.Count > 0)
            {
                return selectedPlayers;
            }

            // Try to create the largest possible group of players including the players already on the server for all that waited longer
            List<MMPlayer> timeoutPlayers = playersForServer.Where(p => (now - p.JoinTime).TotalSeconds >= FRESH_LOBBY_TIMEOUT_SECONDS).ToList();
            if (timeoutPlayers.Count == 0 ||
                timeoutPlayers.Count < playersForServer.Count &&
                timeoutPlayers.Count < timeoutPlayers.Take(timeoutPlayers.Count).Max(d => d.SearchPreferences.MinPlayers))
            {
                // If there are other players, wait until either all time out or enough to create a match amongst them (without joining players).
                // The latter will be the case, if there still are non-timeout players in-between that have higher min thresholds and
                // therefore no match can be created with them. Here we check just among the timeout players (normally we would not have to do this,
                // because they will be sorted by join time, but we have to sort by threshold)

                // This prevents overlapping queue times failing to aggregate players
                // (e.g. player gets joined alone because others entered matchmaking later and have not timeouted yet and we do not wait)
                return [];
            }

            selectedPlayers = SelectMaxPlayersForMatch(timeoutPlayers, joinedPlayersCount, freeSlots);
            if (selectedPlayers.Count > 0)
            {
                return selectedPlayers;
            }

            return selectedPlayers;
        }

        internal static List<MMPlayer> SelectMaxPlayersForMatch(IReadOnlyList<MMPlayer> queuedPlayers, int joinedPlayersCount, int freeSlots)
        {
            List<MMPlayer> bestMatch = [];
            int maxPossiblePlayers = Math.Min(queuedPlayers.Count + joinedPlayersCount, freeSlots);
            int maxTotalPlayers = joinedPlayersCount == 0 ? maxPossiblePlayers : queuedPlayers.Count + joinedPlayersCount;
            int highestMinThreshold = 0;

            List<MMPlayer> currentMatch = new(maxPossiblePlayers);

            for (int i = 0; i < Math.Min(maxPossiblePlayers, queuedPlayers.Count); i++)
            {
                MMPlayer player = queuedPlayers[i];
                currentMatch.Add(player);

                // Update the highest min treshold
                int minTresholdForPlayer = player.SearchPreferences.MinPlayers;
                if (minTresholdForPlayer > highestMinThreshold)
                {
                    highestMinThreshold = minTresholdForPlayer;
                    if (highestMinThreshold > maxTotalPlayers)
                    {
                        // Match is not possible anymore, we can return early
                        return bestMatch;
                    }
                }

                // Ensure that the queued players meet their threshold
                if (currentMatch.Count + joinedPlayersCount >= highestMinThreshold && currentMatch.Count >= bestMatch.Count)
                {
                    bestMatch = new(currentMatch);
                }
            }

            return bestMatch;
        }

        private void RemovePlayer(MMPlayer mmPlayer)
        {
            _playerQueue.Remove(mmPlayer);

            foreach (var srv in mmPlayer.PreferredServers.Keys)
            {
                if (_serverGroups.TryGetValue(srv, out ConcurrentLinkedQueue<MMPlayer>? playersForServer))
                {
                    playersForServer.Remove(mmPlayer);

                    if (playersForServer.Count == 0)
                    {
                        _serverGroups.TryRemove(srv, out _);
                    }
                }
            }
        }

        private async Task<int> CreateMatchAsync(MMMatch match)
        {
            _logger.LogInformation("Match created on server {server} for {numPlayers} players: {players}",
                match.Server, match.SelectedPlayers.Count, match.SelectedPlayers.Select(p => p.Player.Name));

            // Remove matched players from the queue and server groups
            foreach (MMPlayer player in match.SelectedPlayers)
            {
                RemovePlayer(player);
            }

            try
            {
                _logger.LogTrace("Notifying players with match result...");

                await _hubContext.Clients.Clients(match.SelectedPlayers.Select(p => p.Player.ConnectionId))
                    .MatchFound(match.Server.LastServerInfo!.HostName, CreateMatchResult(match));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while notifying players with match result");
            }

            _logger.LogDebug("Joining players to server queue...");

            IEnumerable<Task<bool>> queueTasks = match.SelectedPlayers.Select(p =>
                QueuePlayer(p.Player, match.Server));

            bool[] results = await Task.WhenAll(queueTasks);
            return results.Count(success => success);
        }

        private async Task<bool> QueuePlayer(Player player, GameServer server)
        {
            try
            {
                if (!await _queueingService.JoinQueue(server, player).ConfigureAwait(false))
                {
                    await _hubContext.Clients.Client(player.ConnectionId)
                        .RemovedFromMatchmaking(MatchmakingError.QueueingFailed)
                        .ConfigureAwait(false);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while queueing player {player}", player);
                return false;
            }
        }
    }
}
