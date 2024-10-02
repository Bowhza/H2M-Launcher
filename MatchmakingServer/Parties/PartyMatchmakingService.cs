using System.Collections.Concurrent;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

using MatchmakingServer.Matchmaking;
using MatchmakingServer.Matchmaking.Models;
using MatchmakingServer.Queueing;

using Microsoft.Extensions.Options;

namespace MatchmakingServer.Parties
{
    /// <summary>
    /// Coordinates the matchmaking and queueing by integrating the party functionality.
    /// Handles party queueing / matchmaking when the leader joins or leaves.
    /// </summary>
    public sealed class PartyMatchmakingService : IDisposable
    {
        private readonly PartyService _partyService;
        private readonly MatchmakingService _matchmakingService;
        private readonly QueueingService _queueingService;
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;

        private readonly ConcurrentDictionary<Party, PartyQueueingContext> _contextMap = [];

        public PartyMatchmakingService(
            PartyService partyService, 
            MatchmakingService matchmakingService, 
            QueueingService queueingService, 
            IOptionsMonitor<ServerSettings> serverSettings)
        {
            _partyService = partyService;
            _partyService.PartyClosed += OnPartyClosed;
            _partyService.PlayerRemovedFromParty += OnPlayerRemovedFromParty;
            _partyService.PartyLeaderChanged += OnPartyLeaderChanged;

            _matchmakingService = matchmakingService;
            _queueingService = queueingService;
            _serverSettings = serverSettings;
        }

        private void OnPartyLeaderChanged(Party party, Player oldLeader, Player newLeader)
        {
            if (_contextMap.TryGetValue(party, out PartyQueueingContext? context) &&
                context.QueuedPlayers.Contains(oldLeader) &&
                context.MatchmakingTicket is not null && 
                !context.MatchmakingTicket.IsComplete)
            {
                // change the active searcher to the new leader
                _matchmakingService.UpdateTicketMetadata(context.MatchmakingTicket, new() { ActiveSearcher = newLeader });
            }
        }

        private void OnPlayerRemovedFromParty(Party party, Player player)
        {
            if (!_contextMap.TryGetValue(party, out PartyQueueingContext? context)
                || !context.QueuedPlayers.Remove(player))
            {
                // no context with that player queued
                return;
            }

            if (player.State is PlayerState.Matchmaking)
            {
                // remove player from matchmaking when he leaves party he was matchmaking with
                _matchmakingService.LeaveMatchmaking(player);
            }
            else if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                // remove player from queue when he leaves party he was queueing with
                _queueingService.LeaveQueue(player, DequeueReason.PartyLeave);
            }

            if (context.QueuedPlayers.Count == 0)
            {
                // cleanup when last player queued is removed
                _contextMap.Remove(party, out _);
            }
        }

        private void OnPartyClosed(Party party)
        {
            if (!_contextMap.TryRemove(party, out PartyQueueingContext? context))
            {
                // no queueing context for this party
                return;
            }

            if (context.MatchmakingTicket is not null &&
                !context.MatchmakingTicket.IsComplete)
            {
                // in any case remove the associated ticket completely when party is closed
                _matchmakingService.LeaveMatchmaking(context.MatchmakingTicket);
            }

            // some players might (already) be in the server queue
            foreach (Player p in context.QueuedPlayers)
            {
                if (p.State is PlayerState.Queued)
                {
                    // remove player from queue when party closed and was matchmaking
                    _queueingService.LeaveQueue(p, DequeueReason.PartyClosed);
                }
            }
        }

        private static HashSet<Player> GetEligiblePartyMembers(Player player)
        {
            Party? ownedParty = player.IsPartyLeader ? player.Party : null;
            if (ownedParty is not null)
            {
                return [..ownedParty.Members.Where(m =>
                        m.QueueingHubId is not null &&
                        m.State is not (PlayerState.Matchmaking or PlayerState.Queued or PlayerState.Joining)) //todo(tb): disallow or merge?
                    .OrderByDescending(m => m.IsPartyLeader)
                ];
            }

            return [player];
        }

        #region Matchmaking

        public bool EnterMatchmaking(Player player, MatchSearchCriteria searchPreferences, string playlistId)
        {
            Playlist? playlist = _serverSettings.CurrentValue.Playlists.FirstOrDefault(p => p.Id.Equals(playlistId, StringComparison.OrdinalIgnoreCase));
            if (playlist?.Servers is null)
            {
                return false;
            }

            return EnterMatchmaking(player, searchPreferences, playlist.Servers, playlist);
        }

        public bool EnterMatchmaking(Player player, MatchSearchCriteria searchPreferences, List<ServerConnectionDetails> preferredServers, Playlist? playlist = null)
        {
            // select players for the ticket
            HashSet<Player> players = GetEligiblePartyMembers(player);

            if (players.Count == 0)
            {
                return false;
            }

            IMMTicket? ticket = _matchmakingService.EnterMatchmaking(
                players,
                searchPreferences,
                preferredServers,
                new MatchmakingService.TicketMetadata() { ActiveSearcher = player, AssociatedPlaylist = playlist });

            if (ticket is null)
            {
                return false;
            }

            // set context
            if (player.Party is not null)
            {
                _contextMap[player.Party] = new()
                {
                    Initiator = player,
                    QueuedPlayers = players,
                    MatchmakingTicket = ticket,
                };
            }

            return true;
        }

        public bool LeaveMatchmaking(Player player)
        {
            if (!player.IsPartyLeader)
            {
                return _matchmakingService.LeaveMatchmaking(player);
            }

            if (!_contextMap.TryRemove(player.Party, out PartyQueueingContext? context))
            {
                return _matchmakingService.LeaveMatchmaking(player);
            }

            return _matchmakingService.LeaveMatchmaking(player, removeTicket: true);
        }

        #endregion


        #region Queueing

        public async Task<bool> JoinQueue(Player player, JoinServerInfo server)
        {
            // select players for the queue
            HashSet<Player> players = GetEligiblePartyMembers(player);
            HashSet<Player> queuedPlayers = new(players.Count);

            if (players.Count == 0)
            {
                return false;
            }

            foreach (Player p in players)
            {
                if (await _queueingService.JoinQueue(server, p))
                {
                    queuedPlayers.Add(p);
                }
            }

            // set context
            if (player.Party is not null)
            {
                _contextMap[player.Party] = new PartyQueueingContext()
                {
                    Initiator = player,
                    QueuedPlayers = queuedPlayers
                };
            }

            return true;
        }

        public void LeaveQueue(Player player)
        {
            if (!player.IsPartyLeader)
            {
                _queueingService.LeaveQueue(player, DequeueReason.UserLeave);
                return;
            }

            if (!_contextMap.TryRemove(player.Party, out PartyQueueingContext? context))
            {
                _queueingService.LeaveQueue(player, DequeueReason.UserLeave);
                return;
            }

            foreach (Player p in context.QueuedPlayers)
            {
                // todo: send disconnect if already joined?
                _queueingService.LeaveQueue(p, DequeueReason.PartyLeave);
            }
        }

        public void Dispose()
        {
            _partyService.PlayerRemovedFromParty -= OnPlayerRemovedFromParty;
            _partyService.PartyClosed -= OnPartyClosed;
            _partyService.PartyLeaderChanged -= OnPartyLeaderChanged;
        }

        #endregion
    }
}
