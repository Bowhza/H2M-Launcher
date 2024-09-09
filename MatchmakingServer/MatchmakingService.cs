using System.Collections.Concurrent;

using H2MLauncher.Core.Services;

using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;

using Nito.Disposables.Internals;

namespace MatchmakingServer
{
    public class MatchmakingService
    {
        private const int QUEUE_TIMEOUT_SECONDS = 12; // Timeout after 30 seconds in queue

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
            public List<(string ip, int port)> PreferredServers { get; set; } // List of "ip:port" strings
            public int MinPlayerThreshold { get; set; } // Minimum players required to start a match
            public DateTime JoinTime { get; set; } // Time the player joined the queue

            public MMPlayer(Player player, List<(string ip, int port)> servers, int minThreshold)
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

                if (!_serverStore.Servers.ContainsKey(key))
                {
                    _serverStore.TryAddServer(ip, port, "");
                }
            }

            AddPlayerToQueue(new MMPlayer(player, preferredServersParsed, minPlayers));

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

            foreach (var server in player.PreferredServers)
            {
                if (!_serverGroups.ContainsKey((server.ip, server.port)))
                {
                    _serverGroups[server] = [];
                }

                _serverGroups[server].Enqueue(player);
            }

            _logger.LogDebug("Player {mmPlayer} entered matchmaking", player);
        }

        class MmServerPriorityComparer : IComparer<GameServer>
        {
            public int Compare(GameServer? x, GameServer? y)
            {
                if (x?.LastServerInfo is null || y?.LastServerInfo is null)
                {
                    return 0;
                }

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

            // Request server info for all servers part of matchmaking rn
            await _gameServerCommunicationService.RequestServerInfoAsync(serversToRequest, (e) =>
            {
                e.Server.LastServerInfo = e.ServerInfo;
                e.Server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;

                respondingServers.Add(e.Server);
            }, requestCancellation.Token);

            // Wait a second for all the responses
            await Task.Delay(1000);

            // Cancel remaining requests
            requestCancellation.Cancel();

            // Sort servers: prioritize fresh servers with low player count
            respondingServers.Sort(new MmServerPriorityComparer());

            _logger.LogDebug("Server info received from {numServers}. Selecting players for matchmaking...", respondingServers.Count);

            // Iterate through prioritized servers
            foreach (GameServer server in respondingServers)
            {
                int availableSlots = Math.Max(0, server.LastServerInfo!.FreeSlots - server.UnavailableSlots);
                if (availableSlots <= 0)
                    continue; // Skip if no free slots are available

                if (!_serverGroups.TryGetValue((server.ServerIp, server.ServerPort), out var playersForServer))
                    continue;

                _logger.LogTrace("{numPlayers} players in matchmaking queue for server {server}, trying to create match...",
                    playersForServer.Count, server);

                _logger.LogTrace("Server has {numPlayers} players and {numAvailableSlots} available slots.",
                    server.LastServerInfo.RealPlayerCount, availableSlots);

                List<MMPlayer> selectedPlayers = SelectMaxPlayersForMatchWithTimeout(playersForServer,
                    server.LastServerInfo.RealPlayerCount,
                    availableSlots);

                if (selectedPlayers.Count > 0)
                {
                    await CreateMatchAsync(selectedPlayers, server);
                }
            }
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
            List<MMPlayer> timeoutPlayers = playersForServer.Where(p => (now - p.JoinTime).TotalSeconds >= QUEUE_TIMEOUT_SECONDS).ToList();
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

            foreach (var srv in mmPlayer.PreferredServers)
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
