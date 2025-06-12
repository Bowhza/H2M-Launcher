using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Social;

using MatchmakingServer.Core.Social;
using MatchmakingServer.Parties;

namespace MatchmakingServer
{
    public class Player()
    {
        public required string Id { get; init; }
        public required string Name { get; set; }
        public required string UserName { get; init; }
        public CancellationToken DisconnectedToken { get; init; }

        /// <summary>
        /// Gets the connection id for the queueing hub.
        /// </summary>
        public string? QueueingHubId { get; set; }

        /// <summary>
        /// Gets the connection id for the party hub.
        /// </summary>
        public string? PartyHubId { get; set; }

        /// <summary>
        /// Gets the connection id for the social hub.
        /// </summary>
        public string? SocialHubId { get; set; }

        public PlayerState State { get; set; }

        public DateTimeOffset? QueuedAt { get; set; }

        public TimeSpan? TimeInQueue => DateTimeOffset.Now - QueuedAt;

        public List<DateTimeOffset> JoinAttempts { get; set; } = [];

        /// <summary>
        /// The server the player is queued or joined.
        /// </summary>
        public GameServer? QueuedServer { get; set; }

        /// <summary>
        /// The server the player is currently playing on.
        /// </summary>
        public GameServer? PlayingServer { get; set; }

        private Party? _party = null;

        /// <summary>
        /// The party the player is currently in;
        /// </summary>
        public Party? Party
        {
            get => _party;
            set
            {
                _party = value;
                PartyChanged?.Invoke(this, value);
            }
        }

        [MemberNotNullWhen(true, nameof(Party))]
        public bool IsPartyLeader => Party?.Leader.Id == Id;

        public event Action<Player, Party?>? PartyChanged;


        #region Social

        public GameStatus GameStatus { get; set; }

        public ConnectedServerInfo? LastConnectedServerInfo { get; set; }

        #endregion

        public override bool Equals(object? obj)
        {
            return obj is Player otherPlayer && otherPlayer.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Name} ({Id} - {State})";
        }
    }
}
