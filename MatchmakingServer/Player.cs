using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Matchmaking.Models;

namespace MatchmakingServer
{
    public class Player
    {
        public required string Name { get; set; }
        public required string Id { get; init; }

        /// <summary>
        /// Gets the connection id for the queueing hub.
        /// </summary>
        public string? QueueingHubId { get; set; }

        /// <summary>
        /// Gets the connection id for the party hub.
        /// </summary>
        public string? PartyHubId { get; set; }

        public PlayerState State { get; set; }

        public DateTimeOffset? QueuedAt { get; set; }

        public TimeSpan? TimeInQueue => DateTimeOffset.Now - QueuedAt;

        public List<DateTimeOffset> JoinAttempts { get; set; } = [];

        /// <summary>
        /// The server the player is queued or joined.
        /// </summary>
        public GameServer? Server { get; set; }

        /// <summary>
        /// The party the player is currently in;
        /// </summary>
        public Party? Party { get; set; }

        [MemberNotNullWhen(true, nameof(Party))]
        public bool IsPartyLeader => Party?.Leader.Id == Id;

        public override bool Equals(object? obj)
        {
            return obj is Player otherPlayer && otherPlayer.Id == Id && otherPlayer.QueueingHubId == QueueingHubId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, QueueingHubId);
        }

        public override string ToString()
        {
            return $"{Name} ({Id} - {State})";
        }
    }
}
