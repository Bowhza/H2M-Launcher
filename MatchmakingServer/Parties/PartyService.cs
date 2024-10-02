using System.Collections.Concurrent;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

using MatchmakingServer.Core.Party;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.Parties
{
    public sealed class PartyService
    {
        private readonly ILogger<PartyService> _logger;
        private readonly IHubContext<PartyHub, IPartyClient> _hubContext;

        private readonly ConcurrentDictionary<string, Party> _parties = [];

        public IReadOnlyCollection<IParty> Parties => new ReadOnlyCollectionWrapper<Party>(_parties.Values);

        public event Action<Party>? PartyClosed;
        public event Action<Party, Player>? PlayerRemovedFromParty;
        public event Action<Party, Player, Player>? PartyLeaderChanged;

        public PartyService(IHubContext<PartyHub, IPartyClient> hubContext, ILogger<PartyService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        private static PartyInfo CreatePartyInfo(Party party)
        {
            return new(party.Id, party.Members.Select(m => new PartyPlayerInfo(m.Id, m.Name, m.IsPartyLeader)).ToList());
        }

        private static string GetPartyGroupName(Party party)
        {
            return $"party_{party.Id}";
        }

        public async Task<PartyInfo?> CreateParty(Player player)
        {
            if (player.Party is not null)
            {
                return null;
            }

            Party party = new(player);

            if (!_parties.TryAdd(party.Id, party))
            {
                // somehow the id already exists?
                return null;
            }

            party.AddPlayer(player);
            await _hubContext.Groups.AddToGroupAsync(player.PartyHubId!, GetPartyGroupName(party));

            return CreatePartyInfo(party);
        }

        public async Task<PartyInfo?> JoinParty(Player player, string partyId)
        {
            if (player.Party?.Id == partyId)
            {
                return null;
            }

            if (!_parties.TryGetValue(partyId, out Party? party))
            {
                return null;
            }

            // leave / close old party first
            await LeaveOrCloseParty(player);

            // add player
            party.AddPlayer(player);
            await _hubContext.Groups.AddToGroupAsync(player.PartyHubId!, GetPartyGroupName(party));

            // notify others of join
            await OthersInPartyGroup(player).OnUserJoinedParty(player.Id, player.Name);

            return CreatePartyInfo(party);
        }

        public async Task<bool> LeaveOrCloseParty(Player player)
        {
            if (player.Party is not Party party)
            {
                return false;
            }

            string partyGroupName = GetPartyGroupName(party);

            if (player.IsPartyLeader)
            {
                // close party
                if (_parties.TryRemove(party.Id, out _))
                {
                    IReadOnlyList<Player> removedPlayers = party.CloseParty();

                    _logger.LogInformation("Closed party {partyId} with {numMembers}", party.Id, removedPlayers.Count);

                    await OthersInPartyGroup(player, partyGroupName).OnPartyClosed();

                    foreach (Player removedPlayer in removedPlayers)
                    {
                        if (removedPlayer.PartyHubId is not null)
                        {
                            await _hubContext.Groups.RemoveFromGroupAsync(removedPlayer.PartyHubId, partyGroupName);
                        }
                    }

                    OnPartyClosed(party);
                }
            }
            else
            {
                // remove user from party
                if (party.RemovePlayer(player))
                {
                    await OthersInPartyGroup(player, partyGroupName).OnUserLeftParty(player.Id);
                }

                await _hubContext.Groups.RemoveFromGroupAsync(player.PartyHubId!, partyGroupName);

                OnRemovedFromParty(player, party);
            }

            return true;
        }

        public async Task JoinServer(Player player, SimpleServerInfo server)
        {
            if (!player.IsPartyLeader)
            {
                return;
            }

            if (player.State is not PlayerState.Connected)
            {
                // ignore joins when player is already joining, that means we already notified
                return;
            }

            // notify others to join the server
            await OthersInPartyGroup(player).OnServerChanged(server);
        }

        public async Task UpdatePlayerName(Player player, string newName)
        {
            if (player.Name.Equals(newName, StringComparison.Ordinal))
            {
                return;
            }

            string oldName = player.Name;
            player.Name = newName;
            _logger.LogInformation("Player name changed from '{oldName}' to '{newName}' for {player}", oldName, newName, player);

            if (player.Party is not null)
            {
                await OthersInPartyGroup(player).OnUserNameChanged(player.Id, newName);
            }
        }

        public async Task<bool> KickPlayer(Player caller, string id)
        {
            if (caller.Id == id)
            {
                // cannot kick self
                return false;
            }

            if (!caller.IsPartyLeader)
            {
                // not a party leader
                return false;
            }

            Party party = caller.Party;
            string partyGroupName = GetPartyGroupName(party);

            Player? memberToRemove = party.Members.FirstOrDefault(m => m.Id == id);
            if (memberToRemove is null)
            {
                // player not found
                return false;
            }

            // remove user from party
            if (party.RemovePlayer(memberToRemove))
            {
                // notify other users that user left
                await _hubContext.Clients.GroupExcept(partyGroupName, memberToRemove.PartyHubId!)
                    .OnUserLeftParty(memberToRemove.Id);

                // notify user that he was kicked
                await _hubContext.Clients.Client(memberToRemove.PartyHubId!).OnKickedFromParty();
            }

            await _hubContext.Groups.RemoveFromGroupAsync(memberToRemove.PartyHubId!, partyGroupName);

            OnRemovedFromParty(memberToRemove, party);

            return true;
        }

        public async Task<bool> ChangeLeader(Player caller, string newLeaderId)
        {
            if (!caller.IsPartyLeader)
            {
                // only party leader can change leader
                return false;
            }

            if (caller.Id == newLeaderId)
            {
                // cannot change leader to self
                return false;
            }

            Party party = caller.Party;
            string partyGroupName = GetPartyGroupName(party);

            Player? newLeader = party.Members.FirstOrDefault(m => m.Id == newLeaderId);
            if (newLeader is null)
            {
                // player not found
                return false;
            }

            if (newLeader == party.Leader)
            {
                // same leader
                return false;
            }

            Player oldLeader = party.ChangeLeader(newLeader);

            await _hubContext.Clients.Group(partyGroupName).OnLeaderChanged(oldLeader.Id, newLeader.Id);

            return true;
        }

        private IPartyClient OthersInPartyGroup(Player player, string? partyGroupName = null)
        {
            return _hubContext.Clients.GroupExcept(partyGroupName ?? GetPartyGroupName(player.Party!), player.PartyHubId!);
        }

        private void OnRemovedFromParty(Player removedPlayer, Party party)
        {
            PlayerRemovedFromParty?.Invoke(party, removedPlayer);
        }

        private void OnPartyClosed(Party party)
        {
            PartyClosed?.Invoke(party);
        }
    }
}
