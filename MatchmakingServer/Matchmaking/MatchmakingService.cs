using System.Collections.Concurrent;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using MatchmakingServer.Queueing;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;

using Nito.Disposables.Internals;

namespace MatchmakingServer
{
    public class MatchmakingService : BackgroundService
    {
        /// <summary>
        /// The time in queue after which a fresh lobby is not exclusively created and 
        /// joined players are also calculated in the min players threshold.
        /// </summary>
        private const int FRESH_LOBBY_TIMEOUT_SECONDS = 20;
        private const int MATCHMAKING_INTERVAL_MS = 3000;

        /// <summary>
        /// Holds the players in matchmaking for each server.
        /// </summary>
        private readonly ConcurrentDictionary<ServerConnectionDetails, ConcurrentLinkedQueue<MMPlayer>> _serverGroups = [];

        /// <summary>
        /// All players queued in matchmaking.
        /// </summary>
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
        }

        /// <summary>
        /// Main loop that checks for matches.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_playerQueue.Count == 0)
                {
                    // wait for players to enter matchmaking
                    await Task.Delay(500, stoppingToken);
                }

                await CheckForMatches(stoppingToken);
                await Task.Delay(MATCHMAKING_INTERVAL_MS, stoppingToken);
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

            Dictionary<ServerConnectionDetails, int> preferredServersParsed = [];
            foreach (string address in preferredServers)
            {
                if (!ServerConnectionDetails.TryParse(address, out ServerConnectionDetails connDetails))
                {
                    continue;
                }

                preferredServersParsed.TryAdd(connDetails, -1);

                // Make sure server is created
                _serverStore.GetOrAddServer(connDetails.Ip, connDetails.Port);
            }

            if (preferredServersParsed.Count == 0)
            {
                return false;
            }

            AddPlayerToQueue(new MMPlayer(player, preferredServersParsed, searchPreferences));
            player.State = PlayerState.Matchmaking;

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

        private void RemovePlayer(MMPlayer mmPlayer)
        {
            mmPlayer.SearchAttempts = 0;
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

        private async Task<List<GameServer>> RefreshServerInfo(IReadOnlyList<GameServer> servers, CancellationToken cancellationToken)
        {
            List<GameServer> respondingServers = new(_serverGroups.Count);
            _logger.LogTrace("Requesting server info for {numServers} servers...", servers.Count);
            try
            {
                // Request server info for all servers part of matchmaking rn
                Task getInfoCompleted = await _gameServerCommunicationService.SendGetInfoAsync(servers, (e) =>
                {
                    e.Server.LastServerInfo = e.ServerInfo;
                    e.Server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;

                    respondingServers.Add(e.Server);
                }, timeoutInMs: 2000, cancellationToken: cancellationToken);

                // Immediately after send info requests send status requests
                Task getStatusCompleted = await _gameServerCommunicationService.SendGetStatusAsync(servers, (e) =>
                {
                    e.Server.LastStatusResponse = e.ServerInfo;
                }, timeoutInMs: 2000, cancellationToken: cancellationToken);

                // Wait for all to complete / time out
                await Task.WhenAll(getInfoCompleted, getStatusCompleted);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // expected timeout
                return respondingServers;
            }

            _logger.LogDebug("Server info received from {numServers}", respondingServers.Count);

            return respondingServers;
        }

        private async Task CheckForMatches(CancellationToken cancellationToken)
        {
            List<GameServer> serversToRequest = _serverGroups.Keys
                .Select(key => _serverStore.Servers.TryGetValue(key, out GameServer? server) ? server : null)
                .WhereNotNull()
                .ToList();

            List<GameServer> respondingServers = await RefreshServerInfo(serversToRequest, cancellationToken);

            // Sort servers by quality score
            List<(GameServer server, double qualityScore)> orderedServers = respondingServers
                .Select(s => (server: s, qualityScore: CalculateServerQuality(s)))
                .OrderByDescending(x => x.qualityScore)
                .ToList();


            _logger.LogDebug("{numPlayers} players in matchmaking queue, selecting players for matchmaking...", _playerQueue.Count);

            List<MMMatch> matches = [];
            foreach (MMPlayer player in _playerQueue)
            {
                player.PossibleMatches.Clear();
            }

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
            } while (_playerQueue.Count > 0);


            // Notify players of theoretically possible matches
            List<Task> notifyTasks = new(_playerQueue.Count);
            foreach (MMPlayer player in _playerQueue)
            {
                player.SearchAttempts++;
                notifyTasks.Add(SendMatchSearchResults(player, player.PossibleMatches));
            }

            await Task.WhenAll(notifyTasks);
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
            if (!_serverGroups.TryGetValue((server.ServerIp, server.ServerPort), out ConcurrentLinkedQueue<MMPlayer>? playersForServer))
                return [];

            // Sort players based on their min player threshold (descending order)
            // and check whether server meets their criteria
            return playersForServer
                .Where(p => p.SearchPreferences.MinPlayers <= server.LastServerInfo?.MaxClients) // rule out impossible treshold directly
                .OrderByDescending(p => p.SearchPreferences.MinPlayers)
                .Select(player => (player, isEligible: player.IsEligibleForServer(server, playersForServer.Count)))
                .ToList();
        }

        internal bool TrySelectMatch(GameServer server, IReadOnlyList<MMPlayer> players, double serverQuality, int availableSlots, out MMMatch match)
        {
            List<MMPlayer> selectedPlayers = SelectMaxPlayersForMatchDesc(
                players,
                server.LastServerInfo!.RealPlayerCount,
                availableSlots);

            if (selectedPlayers.Count > 0)
            {
                double adjustedQualityScore = AdjustedServerQuality(server, serverQuality, selectedPlayers, _logger);
                match = (server, adjustedQualityScore, selectedPlayers);

                return true;
            }

            match = default;
            return false;
        }

        internal static double AdjustedServerQuality(GameServer server, double qualityScore, List<MMPlayer> potentialPlayers, ILogger logger)
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

            logger.LogTrace("Adjusting quality based on avg wait time ({avgWaitTime} s) and ping deviation ({avgPingDeviation} ms)",
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

        /// <summary>
        /// Selects the upper max players for a match whose <see cref="MatchSearchCriteria.MinPlayers"/> are satisfied,
        /// given a list of players ordered by their min treshold in descending order.
        /// </summary>
        /// <param name="queuedPlayers">Players to select from ordered by min player treshold in descending order.</param>
        /// <param name="joinedPlayersCount">Number of players alredy on the server.</param>
        /// <param name="freeSlots">The number of free slots available on the server.</param>
        /// <returns>The biggest possible selection of players that can be joined.</returns>
        internal static List<MMPlayer> SelectMaxPlayersForMatchDesc(IReadOnlyList<MMPlayer> queuedPlayers, int joinedPlayersCount, int freeSlots)
        {
            List<MMPlayer> selectedPlayers = [];

            int premain = queuedPlayers.Count; // how many players remain in the queue to consider (starting with all)
            int pjoin = Math.Min(queuedPlayers.Count, freeSlots); // how many players to pull from the queue
            int ptotal = pjoin + joinedPlayersCount; // max total number of players
            int iMaxTreshold; // the index of the player with the max satisfyable treshold

            // iterate over players sorted by min player treshold, starting with the highest treshold
            // and skip until the max treshold for which enough players exist
            for (iMaxTreshold = 0; iMaxTreshold < queuedPlayers.Count; iMaxTreshold++, premain--, pjoin = Math.Min(premain, freeSlots))
            {
                if (queuedPlayers[iMaxTreshold].SearchPreferences.MinPlayers <= (pjoin + joinedPlayersCount))
                {
                    // this and all following players have min treshold <= how many they are
                    break;
                }
            }

            // then take pjoin players starting at the max treshold found
            for (int i = 0; i < pjoin; i++)
            {
                selectedPlayers.Add(queuedPlayers[iMaxTreshold + i]);
            }

            return selectedPlayers;
        }

        internal static List<MMPlayer> SelectMaxPlayersForMatch(IReadOnlyList<MMPlayer> queuedPlayers, int joinedPlayersCount, int freeSlots)
        {
            int maxPossiblePlayers = Math.Min(queuedPlayers.Count + joinedPlayersCount, freeSlots);
            int highestMinThreshold = 0;

            List<MMPlayer> bestMatch = [];
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
                    if (highestMinThreshold > maxPossiblePlayers)
                    {
                        // Match is not possible anymore, we can return early
                        return bestMatch;
                    }
                }

                // Ensure that the queued players meet their threshold
                if (currentMatch.Count + joinedPlayersCount >= highestMinThreshold)
                {
                    bestMatch = new(currentMatch);
                }
            }

            return bestMatch;
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
