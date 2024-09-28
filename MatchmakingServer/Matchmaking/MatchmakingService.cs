using System.Collections.Concurrent;

using H2MLauncher.Core;
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
        private readonly IGameServerInfoService<GameServer> _gameServerInfoService;
        private readonly ILogger<MatchmakingService> _logger;

        private readonly ConcurrentDictionary<IMMTicket, TicketMetadata> _metadata = [];

        public readonly record struct TicketMetadata
        {
            public required Player ActiveSearcher { get; init; }

            public Playlist? AssociatedPlaylist { get; init; }
        }

        public MatchmakingService(
            ServerStore serverStore,
            IHubContext<QueueingHub, IClient> hubContext,
            QueueingService queueingService,
            GameServerCommunicationService<GameServer> gameServerCommunicationService,
            ILogger<MatchmakingService> logger,
            IGameServerInfoService<GameServer> gameServerInfoService,
            Matchmaker matchmaker)
        {
            _serverStore = serverStore;
            _hubContext = hubContext;
            _queueingService = queueingService;
            _gameServerCommunicationService = gameServerCommunicationService;
            _logger = logger;
            _gameServerInfoService = gameServerInfoService;
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
                // Request server info for all servers part of matchmaking rn
                Task getInfoCompleted = await _gameServerInfoService.SendGetInfoAsync(servers, (e) =>
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

        private MMTicket? PrepareTicketWithMetadata(
             IReadOnlySet<Player> players, TicketMetadata ticketMetadata, MatchSearchCriteria searchPreferences, List<ServerConnectionDetails> servers)
        {
            ValidateMetadata(players, servers, ticketMetadata);

            Dictionary<ServerConnectionDetails, int> serversParsed = [];
            foreach (ServerConnectionDetails connDetails in servers)
            {
                if (serversParsed.TryAdd(connDetails, -1))
                {
                    // Make sure server is created
                    _serverStore.GetOrAddServer(connDetails.Ip, connDetails.Port);
                }
            }

            if (serversParsed.Count == 0)
            {
                return null;
            }

            MMTicket ticket = new(players, serversParsed, searchPreferences);

            _metadata.TryAdd(ticket, ticketMetadata);

            return ticket;
        }

        private static void ValidateMetadata(IReadOnlySet<Player> players, IEnumerable<ServerConnectionDetails> servers, TicketMetadata ticketMetadata)
        {
            // validate metadata
            if (!players.Contains(ticketMetadata.ActiveSearcher))
            {
                throw new ArgumentException("Active searcher not part of provided player set.", nameof(ticketMetadata));
            }

            if (ticketMetadata.AssociatedPlaylist?.Servers is not null &&
                servers.Any(s => !ticketMetadata.AssociatedPlaylist.Servers.Contains(s)))
            {
                throw new ArgumentException("Server is not in playlist", nameof(ticketMetadata));
            }
        }

        private bool QueueTicket(MMTicket ticket)
        {
            if (ticket.Players.Any(p => p.State is not (PlayerState.Connected or PlayerState.Joined)))
            {
                return false;
            }

            _matchmaker.AddTicketToQueue(ticket);

            // update state
            foreach (Player p in ticket.Players)
            {
                p.State = PlayerState.Matchmaking;
            }

            // notify other participants they entered
            if (ticket.Players.Count > 1)
            {
                if (!_metadata.TryGetValue(ticket, out TicketMetadata ticketMetadata))
                {
                    ticketMetadata = new()
                    {
                        ActiveSearcher = ticket.Players.First()
                    };
                }

                MatchmakingMetadata metadata = new()
                {
                    IsActiveSearcher = false,
                    TotalGroupSize = ticket.Players.Count,
                    QueueType = ticket.Players.Count > 0 ? MatchmakingQueueType.Party : MatchmakingQueueType.Solo,
                    JoinTime = ticket.JoinTime,
                    SearchPreferences = ticket.SearchPreferences,
                    Playlist = ticketMetadata.AssociatedPlaylist
                };

                _ = NotifyPlayersMachmakingEntered(ticket.Players.Where(p => p != ticketMetadata.ActiveSearcher), metadata);
            }

            return true;
        }

        private bool DequeueTicket(MMTicket ticket)
        {
            if (ticket.MatchCompletion.Task.IsCompleted)
            {
                // ticket is already completed and therfore not owned by the Matchmaker anymore
                return false;
            }

            // remove whole ticket
            if (!_matchmaker.RemoveTicket(ticket))
            {
                _logger.LogWarning("Matchmaking ticket {ticket} could not be removed.", ticket);
                return false;
            }

            _metadata.Remove(ticket, out _);

            // update state
            foreach (Player p in ticket.Players)
            {
                if (p.State is not PlayerState.Matchmaking)
                {
                    // skip player with other state
                    continue;
                }

                p.State = PlayerState.Connected;
            }

            // notify participants of removal
            _ = NotifyPlayersRemovedFromMatchmaking(ticket.Players, MatchmakingError.UserLeave);

            return true;
        }

        /// <summary>
        /// Creates a matchmaking ticket with the <paramref name="players"/> and adds them to the matchmaking queue.
        /// </summary>
        /// <param name="players">The players to queue together.</param>
        /// <param name="searchPreferences">Initial search criteria.</param>
        /// <returns>The matchmaking ticket, if successful.</returns>
        public IMMTicket? EnterMatchmaking(
            IReadOnlySet<Player> players,
            MatchSearchCriteria searchPreferences,
            List<ServerConnectionDetails> servers,
            TicketMetadata ticketMetadata = default)
        {
            if (players.Any(p => p.State is not (PlayerState.Connected or PlayerState.Joined)))
            {
                return null;
            }

            MMTicket? ticket = PrepareTicketWithMetadata(players, ticketMetadata, searchPreferences, servers);
            if (ticket is null)
            {
                return null;
            }

            if (QueueTicket(ticket))
            {
                return ticket;
            }

            return null;
        }

        /// <summary>
        /// Creates a matchmaking ticket with the <paramref name="player"/> and adds him to the matchmaking queue.
        /// </summary>
        /// <returns>The matchmaking ticket, if successful.</returns>
        public IMMTicket? EnterMatchmaking(
            Player player, 
            MatchSearchCriteria searchPreferences, 
            List<ServerConnectionDetails> servers, 
            TicketMetadata ticketMetadata = default)
        {
            if (player.State is not (PlayerState.Connected or PlayerState.Joined))
            {
                // invalid player state
                _logger.LogDebug("Cannot enter matchmaking: invalid state {player}", player);
                return null;
            }

            _logger.LogDebug("Entering matchmaking for player {player} (searchPreferences: {@searchPreferences}, servers: {numPreferredServers})",
                player, searchPreferences, servers.Count);

            MMTicket? ticket = PrepareTicketWithMetadata(new HashSet<Player>(), ticketMetadata, searchPreferences, servers);
            if (ticket is null)
            {
                return null;
            }

            if (QueueTicket(ticket))
            {
                return ticket;
            }

            return null;
        }

        /// <summary>
        /// Removes the <paramref name="ticket"/> from the matchmaking queue.
        /// </summary>
        public bool LeaveMatchmaking(IMMTicket ticket)
        {
            MMTicket? internalTicket = _matchmaker.FindTicketById(ticket.Id);
            if (internalTicket is null)
            {
                return false;
            }

            // remove whole ticket
            if (!DequeueTicket(internalTicket))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the ticket associated with the <paramref name="player"/> and either removes the player 
        /// or the whole ticket from the matchmaking queue.
        /// </summary>
        /// <param name="player">The player to remove.</param>
        /// <param name="removeTicket">Whether to remove the whole associated ticket.</param>
        public bool LeaveMatchmaking(Player player, bool removeTicket = false)
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

            if (ticket.Players.Count == 1 || removeTicket)
            {
                // remove whole ticket
                if (!DequeueTicket(ticket))
                {
                    return false;
                }
            }
            else
            {
                // remove from ticket
                ticket.RemovePlayer(player);
                player.State = PlayerState.Connected;
            }

            if (player.QueueingHubId is not null)
            {
                _hubContext.Clients.Client(player.QueueingHubId)
                    .OnRemovedFromMatchmaking(MatchmakingError.UserLeave);
            }

            _logger.LogInformation("Player {player} removed from matchmaking", player);
            return true;
        }

        /// <summary>
        /// Updates the metadata for the given <paramref name="ticket"/> to <paramref name="ticketMetadata"/>.
        /// </summary>
        public void UpdateTicketMetadata(IMMTicket ticket, TicketMetadata ticketMetadata)
        {
            ValidateMetadata(ticket.Players, ticket.PreferredServers, ticketMetadata);

            if (_metadata.TryGetValue(ticket, out TicketMetadata oldTicketMetadata))
            {
                _metadata[ticket] = ticketMetadata;

                _ = NotifyMatchmakingMetadata(ticket.Players.Where(p => p != oldTicketMetadata.ActiveSearcher),
                    new MatchmakingMetadata()
                    {
                        IsActiveSearcher = false,
                        JoinTime = ticket.JoinTime,
                        TotalGroupSize = ticket.Players.Count,
                        QueueType = ticket.Players.Count > 0 ? MatchmakingQueueType.Party : MatchmakingQueueType.Solo,
                        SearchPreferences = ticket.SearchPreferences,
                        Playlist = ticketMetadata.AssociatedPlaylist
                    });
            }
        }

        /// <summary>
        /// Updates the current search criteria and server pings of the ticket for the active searcher <paramref name="player"/>.
        /// </summary>
        /// <param name="player">The active searcher of the ticket to update.</param>
        /// <param name="matchSearchPreferences">The new match search criteria.</param>
        /// <param name="serverPings">Updated list of server pings.</param>
        /// <returns>True if the update was successful.</returns>
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

            TicketMetadata ticketMetadata = default;

            if (ticket.Players.Count > 0 &&
                _metadata.TryGetValue(ticket, out ticketMetadata) &&
                ticketMetadata.ActiveSearcher != player)
            {
                _logger.LogDebug("Only the active searcher can update multi player ticket perferences");
                return false;
            }

            _logger.LogTrace("Updating search preferences for {player}: {searchPreferences} ({numServerPings} server pings)",
                player, matchSearchPreferences, serverPings.Count);

            ticket.SearchPreferences = matchSearchPreferences;
            
            _ = NotifyMatchmakingMetadata(ticket.Players.Where(p => p != player),
                    new MatchmakingMetadata()
                    {
                        IsActiveSearcher = false,
                        JoinTime = ticket.JoinTime,
                        TotalGroupSize = ticket.Players.Count,
                        QueueType = ticket.Players.Count > 0 ? MatchmakingQueueType.Party : MatchmakingQueueType.Solo,
                        SearchPreferences = ticket.SearchPreferences,
                        Playlist = ticketMetadata.AssociatedPlaylist
                    });

            foreach ((string serverIp, int serverPort, uint ping) in serverPings)
            {
                ticket.PreferredServers[(serverIp, serverPort)] = Math.Min(999, (int)ping);
            }

            return true;
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
                    .OnMatchFound(match.Server.LastServerInfo!.HostName, CreateMatchResult(match));
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
                        .OnRemovedFromMatchmaking(MatchmakingError.QueueingFailed)
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

        private async Task SendMatchSearchResults(MMTicket ticket, List<MMMatch> matchesForPlayer)
        {
            try
            {
                await _hubContext.Clients.Clients(ticket.Players.Select(p => p.QueueingHubId!))
                        .OnSearchMatchUpdate(matchesForPlayer.Select(CreateMatchResult))
                        .ConfigureAwait(false);
            }
            catch
            {
                _logger.LogWarning("Could not send match search results to ticket {ticket}", ticket);
            }
        }

        private async Task NotifyPlayersMachmakingEntered(IEnumerable<Player> players, MatchmakingMetadata metadata)
        {
            try
            {
                IEnumerable<string> connectionIds = players.Select(p => p.QueueingHubId).WhereNotNull();

                await _hubContext.Clients.Clients(connectionIds)
                    .OnMatchmakingEntered(metadata)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while notifying player entered matchmaking");
            }
        }

        private async Task NotifyMatchmakingMetadata(IEnumerable<Player> players, MatchmakingMetadata metadata)
        {
            try
            {
                _logger.LogDebug("Notifying players of matchmaking metadata update...");

                IEnumerable<string> connectionIds = players.Select(p => p.QueueingHubId).WhereNotNull();

                await _hubContext.Clients.Clients(connectionIds)
                    .OnMetadataUpdate(metadata)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while notifying players of metadata update");
            }
        }

        private async Task NotifyPlayersRemovedFromMatchmaking(IEnumerable<Player> players, MatchmakingError reason)
        {
            try
            {
                IEnumerable<string> connectionIds = players.Select(p => p.QueueingHubId).WhereNotNull();

                await _hubContext.Clients.Clients(connectionIds)
                    .OnRemovedFromMatchmaking(reason)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while notifying player removed from matchmaking");
            }
        }

        public IReadOnlyList<Player> GetPlayersInServer(IServerConnectionDetails serverConnectionDetails)
        {
            return _matchmaker.GetPlayersInServer(serverConnectionDetails);
        }
    }
}
