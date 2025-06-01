using System.Collections.Concurrent;
using System.Numerics;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

using MatchmakingServer.Core.Party;
using MatchmakingServer.SignalR;
using MatchmakingServer.Social;

using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.Parties
{
    public sealed class PartyService
    {
        private readonly ILogger<PartyService> _logger;
        private readonly IHubContext<PartyHub, IPartyClient> _hubContext;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ConcurrentDictionary<string, Party> _parties = [];
        private readonly TimeSpan _inviteValidityDuration = TimeSpan.FromMinutes(2);

        // subject for pushing invite expiration notifications
        private readonly Subject<(string PartyId, string PlayerId)> _inviteExpiredSubject = new();

        public IReadOnlyCollection<IParty> Parties => new ReadOnlyCollectionWrapper<Party>(_parties.Values);

        public event Action<Party>? PartyCreated;
        public event Action<Party, IReadOnlyCollection<Player>>? PartyClosed;
        public event Action<Party, PartyPrivacy>? PartyPrivacyChanged;
        public event Action<Party, Player>? PlayerRemovedFromParty;
        public event Action<Party, Player, Player>? PartyLeaderChanged;
        public event Action<Party, Player>? PlayerJoinedParty;

        public PartyService(IHubContext<PartyHub, IPartyClient> hubContext, IServiceScopeFactory serviceScopeFactory, ILogger<PartyService> logger)
        {
            _hubContext = hubContext;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;

            // Subscribe to invite expirations and handle notifications
            _inviteExpiredSubject
                .GroupBy(notification => notification.PlayerId)
                .SelectMany(grp => grp.Throttle(TimeSpan.FromSeconds(1))) // Throttle to avoid too many rapid notifications
                .Subscribe(async notification =>
                {
                    try
                    {
                        await NotifyInviteExpired(notification.PartyId, notification.PlayerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while notify user {playerId} of expired party invite to party {partyId}",
                            notification.PlayerId, notification.PartyId);
                    }
                });
        }

        public IParty? GetPartyById(string partyId)
        {
            return _parties.GetValueOrDefault(partyId);
        }

        public static PartyInfo CreatePartyInfo(IParty party)
        {
            // Only include currently valid invites in the DTO
            List<InviteInfo> validInvites = party.Invites
                                    .Where(kv => DateTime.UtcNow < kv.Value)
                                    .Select(kv => new InviteInfo(kv.Key, kv.Value))
                                    .ToList();
            return new(
                party.Id,
                party.Privacy,
                party.Members.Select(m => new PartyPlayerInfo(m.Id, m.Name, m.UserName, m.IsPartyLeader)).ToList(),
                validInvites);
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

            OnPartyCreated(party);

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

            if (party.Privacy is PartyPrivacy.Closed && !IsValidInvite(party, player.Id))
            {
                _logger.LogDebug("Player {player} tried to join closed party {partyId} without a valid invite", player, partyId);
                return null;
            }

            if (party.Privacy is PartyPrivacy.Friends && !IsValidInvite(party, player.Id))
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var friendshipsService = scope.ServiceProvider.GetRequiredService<FriendshipsService>();

                // verify that the player is friends with the leader
                if (!await friendshipsService.UsersAreFriends(Guid.Parse(player.Id), Guid.Parse(party.Leader.Id)))
                {
                    _logger.LogDebug("Player {player} tried to join friends only party {partyId} of stranger {leader}",
                        player, partyId, party.Leader);
                    return null;
                }
            }

            // leave / close old party first
            await LeaveOrCloseParty(player);

            // add player
            party.AddPlayer(player);
            await _hubContext.Groups.AddToGroupAsync(player.PartyHubId!, GetPartyGroupName(party));

            // notify others of join
            await OthersInPartyGroup(player).OnUserJoinedParty(player.Id, player.UserName, player.Name);

            OnPartyJoined(party, player);

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

                    OnPartyClosed(party, removedPlayers);
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

            OnPartyLeaderChanged(party, oldLeader, newLeader);

            return true;
        }

        public async Task<bool> ChangePartyPrivacy(Player caller, PartyPrivacy newPartyPrivacy)
        {
            if (!caller.IsPartyLeader)
            {
                // only party leader can change privacy
                return false;
            }

            Party party = caller.Party;
            PartyPrivacy oldPartyPrivacy = party.Privacy;
            if (oldPartyPrivacy == newPartyPrivacy)
            {
                // party privacy is already same
                return true;
            }

            party.Privacy = newPartyPrivacy;

            // notify others of changed privacy
            await OthersInPartyGroup(caller).OnPartyPrivacyChanged(oldPartyPrivacy, newPartyPrivacy);

            OnPartyPrivacyChanged(party, newPartyPrivacy);

            return true;
        }

        public async Task<InviteInfo?> CreateInvite(Player caller, Player invitedPlayer)
        {
            if (caller.Party is not Party party)
            {
                return null;
            }

            if (!caller.IsPartyLeader)
            {
                // only party leader can create invites (for now)
                _logger.LogWarning("Player {leaderId} tried to create invite for party {partyId} but is not the leader", caller.Id, party.Id);
                return null;
            }

            // Ensure the invited player is not already in the party
            if (party.Members.Any(m => m.Id == invitedPlayer.Id))
            {
                _logger.LogWarning("Cannot invite. Player {invitedPlayerId} is already in party {partyId}.", invitedPlayer.Id, party.Id);
                return null;
            }

            DateTime expirationTime = DateTime.UtcNow.Add(_inviteValidityDuration);

            // Add invite to the party session
            party.AddInvite(invitedPlayer, expirationTime);

            _logger.LogInformation("Invite created for player {invitedPlayerId} to party {partyId}, expires at {expirationTime}",
                invitedPlayer.Id, party.Id, expirationTime);

            // Notify the invited player of the invite
            await _hubContext.Clients.Client(invitedPlayer.PartyHubId!)
                .OnPartyInviteReceived(new PartyInvite()
                {
                    PartyId = party.Id,
                    SenderId = caller.Id,
                    SenderName = caller.Name,
                    ExpirationTime = expirationTime
                });

            // Schedule the invite expiration notification
            // The .Where ensures we only process if the invite still exists and matches the expected expiration
            // (i.e., hasn't been accepted or removed by other means)
            Observable.Timer(expirationTime - DateTime.UtcNow)
                      .Where(_ => IsInviteStillActive(party.Id, invitedPlayer.Id, expirationTime))
                      .Subscribe(_ => _inviteExpiredSubject.OnNext((party.Id, invitedPlayer.Id)));

            return new InviteInfo(invitedPlayer.Id, expirationTime);
        }

        private static bool IsValidInvite(Party party, string playerId)
        {
            if (party.Invites.TryGetValue(playerId, out DateTime expirationTime))
            {
                return DateTime.UtcNow < expirationTime;
            }
            return false;
        }

        // Helper for Rx.NET to check if invite is still active before notifying expiration
        private bool IsInviteStillActive(string partyId, string playerId, DateTime scheduledExpirationTime)
        {
            if (!_parties.TryGetValue(partyId, out Party? party)) return false;

            if (party.IsClosed) return false;

            if (party.Invites.TryGetValue(playerId, out DateTime currentExpirationTime))
            {
                // Ensure it's the exact same invite we scheduled (prevents issues if a new invite was sent)
                return currentExpirationTime == scheduledExpirationTime;
            }

            return false;
        }

        private async Task NotifyInviteExpired(string partyId, string playerId)
        {
            _logger.LogInformation("Invite for player {playerId} to party {partyId} has expired.", playerId, partyId);

            // Ensure the invite is removed from the party's internal state
            if (_parties.TryGetValue(partyId, out Party? party))
            {
                party.RemoveInvite(playerId);
            }

            // Notify the client
            await _hubContext.Clients.User(playerId).OnPartyInviteExpired(partyId);
        }

        private IPartyClient OthersInPartyGroup(Player player, string? partyGroupName = null)
        {
            return _hubContext.Clients.GroupExcept(partyGroupName ?? GetPartyGroupName(player.Party!), player.PartyHubId!);
        }

        private void OnPartyLeaderChanged(Party party, Player oldLeader, Player newLeader)
        {
            PartyLeaderChanged?.Invoke(party, oldLeader, newLeader);
        }

        private void OnRemovedFromParty(Player removedPlayer, Party party)
        {
            PlayerRemovedFromParty?.Invoke(party, removedPlayer);
        }

        private void OnPartyCreated(Party party)
        {
            PartyCreated?.Invoke(party);
        }

        private void OnPartyClosed(Party party, IReadOnlyCollection<Player> removedPlayers)
        {
            PartyClosed?.Invoke(party, removedPlayers);
        }

        private void OnPartyJoined(Party party, Player joinedPlayer)
        {
            PlayerJoinedParty?.Invoke(party, joinedPlayer);
        }

        private void OnPartyPrivacyChanged(Party party, PartyPrivacy partyPrivacy)
        {
            PartyPrivacyChanged?.Invoke(party, partyPrivacy);
        }
    }
}
