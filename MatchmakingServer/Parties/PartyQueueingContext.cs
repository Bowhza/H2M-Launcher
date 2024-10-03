using MatchmakingServer.Matchmaking.Models;

namespace MatchmakingServer.Parties
{
    public sealed class PartyQueueingContext
    {
        /// <summary>
        /// The player that (initially) initiated the queueing / matchmaking.
        /// </summary>
        public required Player Initiator { get; init; }

        /// <summary>
        /// The players of the party that are together in a matchmaking / server queue.
        /// </summary>
        public required HashSet<Player> QueuedPlayers { get; init; }

        /// <summary>
        /// The associated matchmaking ticket, if not yet completed.
        /// </summary>
        public IMMTicket? MatchmakingTicket { get; init; }
    }
}
