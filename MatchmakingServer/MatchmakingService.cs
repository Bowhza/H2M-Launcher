using System.Collections.Concurrent;

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
        private const int FRESH_LOBBY_TIMEOUT_SECONDS = 15;

        private readonly ConcurrentDictionary<(string ip, int port), ConcurrentLinkedQueue<MMPlayer>> _serverGroups = [];
        private readonly ConcurrentLinkedQueue<MMPlayer> _playerQueue = new();

        private readonly ServerStore _serverStore;
        private readonly IHubContext<QueueingHub> _hubContext;
        private readonly QueueingService _queueingService;
        private readonly GameServerCommunicationService<GameServer> _gameServerCommunicationService;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(
            ServerStore serverStore,
            IHubContext<QueueingHub> hubContext,
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
                    await Task.Delay(3000);
                }
            });
        }

        internal sealed class MMPlayer
        {
            public Player Player { get; }
            public Dictionary<(string ip, int port), int> PreferredServers { get; set; } // List of "ip:port" strings
            public int MinPlayerThreshold { get; set; } // Minimum players required to start a match
            public DateTime JoinTime { get; set; } // Time the player joined the queue

            public MMPlayer(Player player, Dictionary<(string ip, int port), int> servers, int minThreshold)
            {
                Player = player;
                PreferredServers = servers;
                MinPlayerThreshold = minThreshold;
                JoinTime = DateTime.Now; // Record the time they joined the queue
            }
        }

        public bool EnterMatchmaking(Player player, int minPlayers, List<string> preferredServers)
        {
            if (player.State is not PlayerState.Connected or PlayerState.Joined)
            {
                // invalid player state
                _logger.LogDebug("Cannot enter matchmaking: invalid state {player}", player);
                return false;
            }

            _logger.LogDebug("Entering matchmaking for player {player} (minPlayers: {minPlayers}, servers: {numPreferredServers})",
                player, minPlayers, preferredServers.Count);

            player.State = PlayerState.Matchmaking;

            List<(string ip, int port)> preferredServersParsed = [];

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

            AddPlayerToQueue(new MMPlayer(player, preferredServersParsed.ToDictionary(x => x, _ => 20), minPlayers));

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
            return true;
        }

        private void AddPlayerToQueue(MMPlayer player)
        {
            _playerQueue.Enqueue(player);

            foreach (var server in player.PreferredServers.Keys)
            {
                if (!_serverGroups.ContainsKey((server.ip, server.port)))
                {
                    _serverGroups[server] = [];
                }

                _serverGroups[server].Enqueue(player);
            }

            _logger.LogDebug("Player {mmPlayer} entered matchmaking", player);
        }

        public double CalculateServerQuality(GameServer server)
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
            //if (totalScoreAssumption == 0 && server.LastStatusResponse!.Players.Count == 0 && server.LastServerInfo.RealPlayerCount)
            //{

            //}

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
                .OrderByDescending(x => x.qualityScore);

            _logger.LogDebug("Selecting players for matchmaking...");

            List<(GameServer server, double qualityScore, List<MMPlayer> players)> matches = [];
            Dictionary<MMPlayer, List<(GameServer server, List<MMPlayer> players)>> matchesByPlayer = [];

            // Iterate through prioritized servers
            foreach ((GameServer server, double qualityScore) in orderedServers)
            {
                int availableSlots = Math.Max(0, server.LastServerInfo!.FreeSlots - server.UnavailableSlots);
                if (availableSlots <= 0)
                    continue; // Skip if no free slots are available

                if (!_serverGroups.TryGetValue((server.ServerIp, server.ServerPort), out var playersForServer))
                    continue;

                _logger.LogTrace("{numPlayers} players in matchmaking queue for server {server}, checking for potential match...",
                    playersForServer.Count, server);

                _logger.LogTrace("Server has {numPlayers} players, {numAvailableSlots} available slots, {totalScore} total score => Quality {qualityScore}",
                    server.LastServerInfo.RealPlayerCount, availableSlots, server.LastStatusResponse?.TotalScore, qualityScore);

                List<MMPlayer> selectedPlayers = SelectMaxPlayersForMatchWithTimeout(playersForServer,
                    server.LastServerInfo.RealPlayerCount,
                    availableSlots);

                if (selectedPlayers.Count > 0)
                {
                    double adjustedQualityScore = AdjustedServerQuality(server, qualityScore, selectedPlayers);
                    matches.Add((server, adjustedQualityScore, selectedPlayers));

                    _logger.LogTrace("Potential match with {numPlayers} players ({numTotalPlayers} total), adjusted quality score {quality}",
                        selectedPlayers.Count, selectedPlayers.Count + server.LastServerInfo.RealPlayerCount, adjustedQualityScore);

                    foreach (MMPlayer player in selectedPlayers)
                    {
                        if (!matchesByPlayer.TryGetValue(player, out var playerMatchesList))
                        {
                            playerMatchesList = [];
                            matchesByPlayer.Add(player, playerMatchesList);
                        }

                        playerMatchesList.Add((server, selectedPlayers));
                    }
                }
            }
            
            var bestMatch = matches.OrderByDescending(x => x.qualityScore).FirstOrDefault();
            if (bestMatch.players is not null)
            {
                await CreateMatchAsync(bestMatch.players, bestMatch.server);
            }
        }

        internal double AdjustedServerQuality(GameServer server, double qualityScore, List<MMPlayer> potentialPlayers)
        {
            DateTime now = DateTime.Now;
            double avgWaitTime = potentialPlayers.Average(p => (now - p.JoinTime).TotalSeconds);
            double waitTimeFactor = 40;

            double avgPing = potentialPlayers.Average(p => p.PreferredServers[(server.ServerIp, server.ServerPort)]);
            double pingFactor = 40;

            _logger.LogTrace("Adjusting quality based on avg wait time ({avgWaitTime} s) and ping ({avgPing} ms)", 
                Math.Round(avgWaitTime, 1), Math.Round(avgPing, 1));

            return qualityScore + (potentialPlayers.Count * 15) + (waitTimeFactor * avgWaitTime) - (pingFactor * avgPing);
        }

        internal static List<MMPlayer> SelectMaxPlayersForMatchWithTimeout(IEnumerable<MMPlayer> queuedPlayers, int joinedPlayersCount, int freeSlots)
        {
            DateTime now = DateTime.Now;

            // Sort players based on their min player threshold (ascending order)
            List<MMPlayer> playersForServer = queuedPlayers.OrderBy(p => p.MinPlayerThreshold).ToList();

            // Try to create the largest possible group of players
            List<MMPlayer> selectedPlayers = SelectMaxPlayersForMatch(playersForServer, 0, freeSlots);
            if (selectedPlayers.Count > 0)
            {
                return selectedPlayers;
            }

            // Try to create the largest possible group of players including the players already on the server for all that waited longer
            List<MMPlayer> timeoutPlayers = playersForServer.Where(p => (now - p.JoinTime).TotalSeconds >= FRESH_LOBBY_TIMEOUT_SECONDS).ToList();
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
                if (player.MinPlayerThreshold > highestMinThreshold)
                {
                    highestMinThreshold = player.MinPlayerThreshold;
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

        private Task<int> CreateMatchAsync(IReadOnlyList<MMPlayer> players, GameServer server)
        {
            _logger.LogInformation("Match created on server {server} for {numPlayers} players: {players}",
                server, players.Count, players.Select(p => p.Player.Name));

            _logger.LogDebug("Joining players to server queue");

            List<Task<bool>> joinTasks = [];

            // Remove matched players from the queue and server groups
            foreach (var player in players)
            {
                RemovePlayer(player);

                joinTasks.Add(_queueingService.JoinQueue(server, player.Player));
            }

            return Task.WhenAll(joinTasks)
                       .ContinueWith(t => t.Result.Count(joined => joined), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
