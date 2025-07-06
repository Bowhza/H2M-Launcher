using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using AsyncKeyedLock;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

using MatchmakingServer.Matchmaking.Models;

namespace MatchmakingServer.Matchmaking;

public class Matchmaker
{
    private readonly ILogger<Matchmaker> _logger;
    private readonly AsyncNonKeyedLocker _semaphore = new(1);

    /// <summary>
    /// Holds the tickets in matchmaking for each server.
    /// </summary>
    private readonly ConcurrentDictionary<ServerConnectionDetails, ConcurrentLinkedQueue<MMTicket>> _serverQueues = [];

    /// <summary>
    /// All players queued in matchmaking.
    /// </summary>
    private readonly ConcurrentLinkedQueue<MMTicket> _queue = new();

    public IReadOnlyCollection<MMTicket> Tickets => _queue;
    public IReadOnlyCollection<ServerConnectionDetails> QueuedServers => new ReadOnlyCollectionWrapper<ServerConnectionDetails>(_serverQueues.Keys);

    public Matchmaker(ILogger<Matchmaker> logger)
    {
        _logger = logger;
    }

    public void AddTicketToQueue(MMTicket ticket)
    {
        _queue.Enqueue(ticket);

        foreach (ServerConnectionDetails server in ticket.PreferredServers.Keys)
        {
            if (!_serverQueues.ContainsKey(server))
            {
                _serverQueues[server] = [];
            }

            _serverQueues[server].Enqueue(ticket);
        }

        _logger.LogDebug("Ticket added to matchmaking queue: {ticket}", ticket);
    }

    public bool RemoveTicket(MMTicket ticket)
    {
        bool removed = _queue.Remove(ticket);

        ticket.MatchCompletion.TrySetCanceled();

        foreach (ServerConnectionDetails server in ticket.PreferredServers.Keys)
        {
            if (_serverQueues.TryGetValue(server, out ConcurrentLinkedQueue<MMTicket>? playersForServer))
            {
                playersForServer.Remove(ticket);

                if (playersForServer.Count == 0)
                {
                    _serverQueues.TryRemove(server, out _);
                }
            }
        }

        _logger.LogDebug("Ticket removed from matchmaking queue: {ticket}", ticket);

        return removed;
    }

    public MMTicket? FindTicketById(Guid ticketId)
    {
        return _queue.FirstOrDefault(t => t.Id == ticketId);
    }

    private MMMatch? CreateNextMatch(IEnumerable<(GameServer, double)> serversWithQuality)
    {
        List<MMMatch> matches = [];

        // Iterate through prioritized servers
        foreach ((GameServer server, double qualityScore) in serversWithQuality)
        {
            int availableSlots = Math.Max(0, server.LastServerInfo!.FreeSlots - server.UnavailableSlots);

            _logger.LogTrace("Server {server} has {numPlayers} players, {numAvailableSlots} available slots, {totalScore} total score => Quality {qualityScore}",
                server, server.LastServerInfo.RealPlayerCount, availableSlots, server.LastStatusResponse?.TotalScore, qualityScore);

            if (availableSlots <= 0)
                continue; // Skip if no free slots are available

            // Sort players based on their min player threshold (ascending order) and check whether servers meets their criteria
            List<(MMTicket ticket, EligibilityResult eligibility)> ticketsForServerSorted = GetTicketsForServer(server);
            if (ticketsForServerSorted.Count == 0)
            {
                // no players
                continue;
            }

            List<MMTicket> eligibleTickets = ticketsForServerSorted
                .Where(x => x.eligibility.IsEligibile)
                .Select(x => x.ticket)
                .ToList();

            _logger.LogTrace("{numTickets} tickets ({numEligible} eligible) in matchmaking queue for server {server}",
                ticketsForServerSorted.Count, eligibleTickets.Count, server);

            // find a valid match for all eligible players
            if (TrySelectMatch(server, eligibleTickets, qualityScore, availableSlots, out MMMatch validMatch))
            {
                matches.Add(validMatch);

                _logger.LogTrace("Potential match found: {validMatch}",
                    new
                    {
                        NumTickets = validMatch.SelectedTickets.Count,
                        NumPlayers = validMatch.SelectedTickets.Sum(t => t.Players.Count),
                        TotalPlayers = validMatch.SelectedTickets.Sum(t => t.Players.Count) + server.LastServerInfo.RealPlayerCount,
                        AdjustedQuality = validMatch.MatchQuality
                    });
            }

            // find overall best possible match for each non eligible player
            foreach ((MMTicket ticket, EligibilityResult eligibility) in ticketsForServerSorted)
            {
                if (eligibility.IsEligibile) continue;

                _logger.LogTrace("Try finding match for ineligible ticket {ticket}, reason: {ineligibilityReason}", ticket, eligibility.Reason);

                bool foundMatch = TrySelectMatch(
                    server,
                    ticketsForServerSorted.Select(x => x.ticket).ToList(),
                    qualityScore,
                    availableSlots,
                    out MMMatch match);

                if (!foundMatch)
                {
                    continue;
                }

                ticket.PossibleMatches.Add(match);

                _logger.LogTrace("Possible match found for ticket {ticket}: {validMatch}",
                    ticket,
                    new
                    {
                        NumTickets = match.SelectedTickets.Count,
                        NumPlayers = match.SelectedTickets.Sum(t => t.Players.Count),
                        TotalPlayers = match.SelectedTickets.Sum(t => t.Players.Count) + server.LastServerInfo.RealPlayerCount,
                        AdjustedQuality = match.MatchQuality
                    });
            }
        }

        if (matches.Count == 0)
        {
            // no more match
            _logger.LogDebug("No more matches found");
            return null;
        }

        // Find match with best quality
        MMMatch bestMatch = matches.OrderByDescending(x => x.MatchQuality).First();

        _logger.LogDebug("Best match found: {bestMatch}", new
        {
            bestMatch.Server,
            NumTickets = bestMatch.SelectedTickets.Count,
            NumPlayers = bestMatch.SelectedTickets.Sum(t => t.Players.Count),
            TotalPlayers = bestMatch.SelectedTickets.Sum(t => t.Players.Count) + bestMatch.Server.LastServerInfo!.RealPlayerCount,
            AdjustedQuality = bestMatch.MatchQuality
        });

        // atomic completion
        using (bestMatch.SelectedTickets.Select(t => t.LockObj).LockAll())
        {
            if (bestMatch.SelectedTickets.Any(t => t.MatchCompletion.Task.IsCompleted))
            {
                _logger.LogWarning("Invalid match: Selected ticket already completed");
                return null;
            }

            // Complete and remove the tickets
            foreach (MMTicket ticket in bestMatch.SelectedTickets)
            {
                ticket.MatchCompletion.TrySetResult(bestMatch);
                RemoveTicket(ticket);
            }
        }

        return bestMatch;
    }

    public async IAsyncEnumerable<MMMatch> CheckForMatchesAsync(IEnumerable<GameServer> servers, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken).ConfigureAwait(false);
        // Sort servers by quality score
        List<(GameServer server, double qualityScore)> orderedServers = servers
            .Select(s => (server: s, qualityScore: CalculateServerQuality(s)))
            .OrderByDescending(x => x.qualityScore)
            .ToList();

        _logger.LogDebug("{numPlayers} players in matchmaking queue, selecting players for matchmaking...", _queue.Count);

        List<MMMatch> matches = [];
        foreach (MMTicket ticket in _queue)
        {
            ticket.PossibleMatches.Clear();
        }

        cancellationToken.ThrowIfCancellationRequested();

        do
        {
            MMMatch? nextMatch = CreateNextMatch(orderedServers);
            if (nextMatch.HasValue)
            {
                yield return nextMatch.Value;
            }
            else
            {
                // no more matches in this pass
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
        } while (_queue.Count > 0);

        foreach (MMTicket ticket in _queue)
        {
            ticket.SearchAttempts++;
        }
    }

    public List<Player> GetPlayersInServer(IServerConnectionDetails serverConnectionDetails)
    {
        List<Player> result = [];
        if (serverConnectionDetails is not ServerConnectionDetails key)
        {
            key = new(serverConnectionDetails.Ip, serverConnectionDetails.Port);
        }
        if (_serverQueues.TryGetValue(key, out ConcurrentLinkedQueue<MMTicket>? queue))
        {
            result.AddRange(queue.SelectMany(t => t.Players));
        }
        return result;
    }

    private List<(MMTicket ticket, EligibilityResult eligibility)> GetTicketsForServer(GameServer server)
    {
        if (!_serverQueues.TryGetValue((server.ServerIp, server.ServerPort), out ConcurrentLinkedQueue<MMTicket>? ticketsForServer))
            return [];

        // Sort players based on their min player threshold (descending order)
        // and check whether server meets their criteria
        return ticketsForServer
            .Where(t => t.SearchPreferences.MinPlayers <= server.LastServerInfo?.MaxClients) // rule out impossible treshold directly
            .OrderByDescending(t => t.SearchPreferences.MinPlayers)
            .Select(ticket => (ticket, eligibility: ticket.IsEligibleForServer(server, ticketsForServer.Sum(t => t.Players.Count))))
            .ToList();
    }

    internal bool TrySelectMatch(GameServer server, IReadOnlyList<MMTicket> tickets, double serverQuality, int availableSlots, out MMMatch match)
    {
        List<MMTicket> selectedTickets = SelectMaxPlayersForMatchDesc(
            tickets,
            server.LastServerInfo!.RealPlayerCount,
            availableSlots);

        if (selectedTickets.Count > 0)
        {
            double adjustedQualityScore = AdjustedServerQuality(server, serverQuality, selectedTickets, _logger);
            match = (server, adjustedQualityScore, selectedTickets);

            return true;
        }

        match = default;
        return false;
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

    internal static double AdjustedServerQuality(GameServer server, double qualityScore, List<MMTicket> potentialPlayers, ILogger logger)
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


    /// <summary>
    /// Selects the upper max players for a match whose <see cref="MatchSearchCriteria.MinPlayers"/> are satisfied,
    /// given a list of players ordered by their min treshold in descending order.
    /// </summary>
    /// <param name="tickets">Players to select from ordered by min player treshold in descending order.</param>
    /// <param name="joinedPlayersCount">Number of players alredy on the server.</param>
    /// <param name="freeSlots">The number of free slots available on the server.</param>
    /// <returns>The biggest possible selection of players that can be joined.</returns>
    internal static List<MMTicket> SelectMaxPlayersForMatchDesc(IReadOnlyList<MMTicket> tickets, int joinedPlayersCount, int freeSlots)
    {
        List<MMTicket> selectedTickets = new(freeSlots);
        int selectedPlayers = 0;

        // outer loop is for the selection start index
        // (first ticket with min players small enough to be satisfied)
        for (int i = 0; i < tickets.Count; i++)
        {
            // adjust min players by subtracting already joined players
            int minPlayers = tickets[i].SearchPreferences.MinPlayers - joinedPlayersCount;
            if (minPlayers > freeSlots)
            {
                // less free slots than min players
                continue;
            }

            // select the maximum amount of players possible, starting at i
            for (int j = i; j < tickets.Count; j++)
            {
                selectedPlayers += tickets[j].Players.Count;
                if (selectedPlayers > freeSlots)
                {
                    // including this ticket would overfill -> stop selecting
                    break;
                }

                selectedTickets.Add(tickets[j]);
            }

            if (minPlayers <= selectedTickets.Count)
            {
                // we found the first (and best) match
                return selectedTickets;
            }

            // not enough players selected
            selectedTickets.Clear();
            selectedPlayers = 0;
        }

        // no valid selection found, return empty
        return selectedTickets;
    }
}
