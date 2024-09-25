using H2MLauncher.Core.Matchmaking.Models;
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

        private readonly Matchmaker _matchmaker;

        private readonly ServerStore _serverStore;
        private readonly IHubContext<QueueingHub, IClient> _hubContext;
        private readonly QueueingService _queueingService;
        private readonly GameServerCommunicationService<GameServer> _gameServerCommunicationService;
        private readonly IGameServerInfoService<GameServer> _tcpGameServerInfoService;
        private readonly IMasterServerService _hmwMasterServerService;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(
            ServerStore serverStore,
            IHubContext<QueueingHub, IClient> hubContext,
            QueueingService queueingService,
            GameServerCommunicationService<GameServer> gameServerCommunicationService,
            ILogger<MatchmakingService> logger,
            [FromKeyedServices("TCP")] IGameServerInfoService<GameServer> tcpGameServerInfoService,
            [FromKeyedServices("HMW")] IMasterServerService hmwMasterServerService,
            Matchmaker matchmaker)
        {
            _serverStore = serverStore;
            _hubContext = hubContext;
            _queueingService = queueingService;
            _gameServerCommunicationService = gameServerCommunicationService;
            _logger = logger;
            _tcpGameServerInfoService = tcpGameServerInfoService;
            _hmwMasterServerService = hmwMasterServerService;
            _matchmaker = matchmaker;
        }

        /// <summary>
        /// Main loop that checks for matches.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_matchmaker.Tickets.Count == 0)
                {
                    // wait for players to enter matchmaking
                    await Task.Delay(500, stoppingToken);
                }

                await CheckForMatches(stoppingToken);
                await Task.Delay(MATCHMAKING_INTERVAL_MS, stoppingToken);
            }
        }

        private async Task CheckForMatches(CancellationToken cancellationToken)
        {
            try
            {
            List<GameServer> serversToRequest = _matchmaker.QueuedServers
                    .Select(key => _serverStore.Servers.TryGetValue(key, out GameServer? server) ? server : null)
                    .WhereNotNull()
                    .ToList();

            List<GameServer> respondingServers = await RefreshServerInfo(serversToRequest, cancellationToken);

            await foreach (MMMatch match in _matchmaker.CheckForMatchesAsync(respondingServers, cancellationToken))
            {
                _ = await CreateMatchAsync(match);
            }

            // Notify players of theoretically possible matches
            List<Task> notifyTasks = new(_matchmaker.Tickets.Count);
            foreach (MMTicket ticket in _matchmaker.Tickets)
            {
                notifyTasks.Add(SendMatchSearchResults(ticket, ticket.PossibleMatches));
            }

            await Task.WhenAll(notifyTasks);
        }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in matchmaking loop");
            }
        }

        private async Task<List<GameServer>> RefreshServerInfo(IReadOnlyList<GameServer> servers, CancellationToken cancellationToken)
        {
            List<GameServer> respondingServers = new(servers.Count);
            _logger.LogTrace("Requesting server info for {numServers} servers...", servers.Count);
            try
            {
                IReadOnlySet<ServerConnectionDetails> hmwServerList = await _hmwMasterServerService.GetServersAsync(cancellationToken);
                List<GameServer> tcpServers = [];
                List<GameServer> udpServers = [];

                foreach (GameServer server in servers)
                {
                    if (hmwServerList.Contains((server.ServerIp, server.ServerPort)))
                    {
                        tcpServers.Add(server);
                    }
                    else
                    {
                        udpServers.Add(server);
                    }
                }

                // Request server info for all servers part of matchmaking rn
                Task getInfoTcpCompleted = await _tcpGameServerInfoService.SendGetInfoAsync(tcpServers, (e) =>
                {
                    e.Server.LastServerInfo = e.ServerInfo;
                    e.Server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;

                    respondingServers.Add(e.Server);
                }, timeoutInMs: 2000, cancellationToken: cancellationToken);

                Task getInfoCompleted = await _gameServerCommunicationService.SendGetInfoAsync(udpServers, (e) =>
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
                await Task.WhenAll(getInfoCompleted, getInfoTcpCompleted, getStatusCompleted);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // expected timeout
                return respondingServers;
            }

            _logger.LogDebug("Server info received from {numServers}", respondingServers.Count);

            return respondingServers;
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

            // select players for the ticket
            List<Player> players;
            if (player.IsPartyLeader)
            {
                // whole party
                players = [.. player.Party.Members.Where(m =>
                    m.QueueingHubId is not null &&
                    m.State is not PlayerState.Matchmaking) //todo(tb): disallow or merge?
                ];
            }
            else
            {
                // alone (player might be in party though, but allow this for now)
                players = [player];
            }

            MMTicket ticket = new(players, preferredServersParsed, searchPreferences);
            _matchmaker.AddTicketToQueue(ticket);

            foreach (Player p in players)
            {
                p.State = PlayerState.Matchmaking;
            }

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

            MMTicket? ticket = _matchmaker.Tickets.FirstOrDefault(t => t.Players.Contains(player));
            if (ticket is null)
            {
                _logger.LogWarning("Player {player} not queued in Matchmaking despite state. Correcting state to 'Connected'.", player);
                player.State = PlayerState.Connected;
                return false;
            }

            if (player.IsPartyLeader || ticket.Players.Count == 1)
            {
                // remove whole ticket
                _matchmaker.RemoveTicket(ticket);
                foreach (Player p in ticket.Players)
                {
                    p.State = PlayerState.Connected;
                }
            }
            else
            {
                // remove from ticket
                ticket.RemovePlayer(player);
                player.State = PlayerState.Connected;
            }

            _logger.LogInformation("Player {player} removed from matchmaking", player);
            return true;
        }

        public bool UpdateSearchPreferences(Player player, MatchSearchCriteria matchSearchPreferences, List<ServerPing> serverPings)
        {
            if (player.State is not PlayerState.Matchmaking)
            {
                // invalid player state
                _logger.LogDebug("Cannot update search session: invalid state {player}", player);
                return false;
            }

            MMTicket? ticket = _matchmaker.Tickets.FirstOrDefault(p => p.Players.Contains(player));
            if (ticket is null)
            {
                _logger.LogWarning("Player {player} not queued in Matchmaking despite state. Correcting state to 'Connected'.", player);
                player.State = PlayerState.Connected;
                return false;
            }

            if (ticket.Players.Count > 1 && !player.IsPartyLeader)
            {
                _logger.LogDebug("Only party leader can update multi player ticket perferences");
                return false;
            }

            _logger.LogTrace("Updating search preferences for {player}: {searchPreferences} ({numServerPings} server pings)",
                player, matchSearchPreferences, serverPings.Count);

            ticket.SearchPreferences = matchSearchPreferences;

            foreach ((string serverIp, int serverPort, uint ping) in serverPings)
            {
                ticket.PreferredServers[(serverIp, serverPort)] = Math.Min(999, (int)ping);
            }

            return true;
        }

        private async Task SendMatchSearchResults(MMTicket ticket, List<MMMatch> matchesForPlayer)
        {
            try
            {
                await _hubContext.Clients.Clients(ticket.Players.Select(p => p.QueueingHubId!))
                        .SearchMatchUpdate(matchesForPlayer.Select(CreateMatchResult))
                        .ConfigureAwait(false);
            }
            catch
            {
                _logger.LogWarning("Could not send match search results to ticket {ticket}", ticket);
            }
        }

        private static SearchMatchResult CreateMatchResult(MMMatch match)
        {
            return new SearchMatchResult()
            {
                ServerIp = match.Server.ServerIp,
                ServerPort = match.Server.ServerPort,
                MatchQuality = match.MatchQuality,
                NumPlayers = match.SelectedTickets.Sum(t => t.Players.Count),
                ServerScore = match.Server.LastStatusResponse?.TotalScore
            };
        }

        private async Task<int> CreateMatchAsync(MMMatch match)
        {
            List<Player> selectedPlayers = match.SelectedTickets.SelectMany(p => p.Players).ToList();

            _logger.LogInformation("Match created on server {server} for {numPlayers} players: {players}",
                match.Server,
                selectedPlayers.Count,
                selectedPlayers.Select(p => p.Name));

            try
            {
                _logger.LogTrace("Notifying players with match result...");

                await _hubContext.Clients.Clients(selectedPlayers.Select(p => p.QueueingHubId!))
                    .MatchFound(match.Server.LastServerInfo!.HostName, CreateMatchResult(match));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while notifying players with match result");
            }

            _logger.LogDebug("Joining players to server queue...");

            IEnumerable<Task<bool>> queueTasks = selectedPlayers.Select(player => QueuePlayer(player, match.Server));

            bool[] results = await Task.WhenAll(queueTasks);
            return results.Count(success => success);
        }

        private async Task<bool> QueuePlayer(Player player, GameServer server)
        {
            try
            {
                if (!await _queueingService.JoinQueue(server, player).ConfigureAwait(false))
                {
                    await _hubContext.Clients.Client(player.QueueingHubId!)
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

        public IReadOnlyList<Player> GetPlayersInServer(IServerConnectionDetails serverConnectionDetails)
        {
            return _matchmaker.GetPlayersInServer(serverConnectionDetails);
        }
    }
}
