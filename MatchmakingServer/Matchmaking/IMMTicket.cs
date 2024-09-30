using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

namespace MatchmakingServer
{
    public interface IMMTicket
    {
        /// <summary>
        /// Unique id for this ticket.
        /// </summary>
        Guid Id { get; init; }

        /// <summary>
        /// Time when this ticket was added to matchmaking.
        /// </summary>
        DateTime JoinTime { get; }

        /// <summary>
        /// Players in this ticket.
        /// </summary>
        IReadOnlySet<Player> Players { get; }

        /// <summary>
        /// Currently possible non eligible matches.
        /// </summary>
        IReadOnlyList<MMMatch> PossibleMatches { get; }

        /// <summary>
        /// Current collection of queued servers.
        /// </summary>
        IReadOnlyCollection<ServerConnectionDetails> PreferredServers { get; }

        /// <summary>
        /// Whether the ticket is complete (either a match has been created or it was revoked).
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// The current number of match search attempts for this ticket.
        /// </summary>
        int SearchAttempts { get; }

        /// <summary>
        /// Gets the current search criteria.
        /// </summary>
        MatchSearchCriteria SearchPreferences { get; }

        Task<MMMatch> WaitForMatchAsync();
    }
}