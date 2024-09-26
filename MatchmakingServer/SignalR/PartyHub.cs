using System.Collections.Concurrent;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

using MatchmakingServer.Core.Party;
using MatchmakingServer.Queueing;

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR;

[Authorize(AuthenticationSchemes = BearerTokenDefaults.AuthenticationScheme)]
class PartyHub : Hub<IPartyClient>, IPartyHub
{
    private static readonly ConcurrentDictionary<string, Party> Parties = [];
    private static readonly ConcurrentDictionary<string, Player> ConnectedPlayers = [];

    private readonly MatchmakingService _matchmakingService;
    private readonly QueueingService _queueingService;
    private readonly PlayerStore _playerStore;
    private readonly ILogger<PartyHub> _logger;

    public PartyHub(
        PlayerStore playerStore,
        ILogger<PartyHub> logger,
        MatchmakingService matchmakingService,
        QueueingService queueingService)
    {
        _playerStore = playerStore;
        _logger = logger;
        _matchmakingService = matchmakingService;
        _queueingService = queueingService;
    }

    private static PartyInfo CreatePartyInfo(Party party)
    {
        return new(party.Id, party.Members.Select(m => new PartyPlayerInfo(m.Id, m.Name, m.IsPartyLeader)).ToList());
    }

    private static string GetPartyGroupName(Party party)
    {
        return $"party_{party.Id}";
    }

    public async Task<PartyInfo?> CreateParty()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return null;
        }

        if (player.Party is not null)
        {
            return null;
        }

        Party party = new()
        {
            Leader = player
        };

        if (!Parties.TryAdd(party.Id, party))
        {
            return null;
        }

        party.AddPlayer(player);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetPartyGroupName(party));

        return CreatePartyInfo(party);
    }

    public async Task<PartyInfo?> JoinParty(string partyId)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return null;
        }

        if (player.Party?.Id == partyId)
        {
            return null;
        }

        if (!Parties.TryGetValue(partyId, out Party? party))
        {
            return null;
        }

        // leave / close old party first
        await LeaveOrCloseParty(player);

        // add player
        party.AddPlayer(player);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetPartyGroupName(party));

        // notify others of join
        await Clients.OthersInGroup(GetPartyGroupName(party)).OnUserJoinedParty(player.Id, player.Name);

        return CreatePartyInfo(party);
    }

    /// <summary>
    /// Cleanup, remove player from matchmaking / queue when leader was in queue and removed from party.
    /// </summary>
    private void OnRemovedFromParty(Player player, Player leader)
    {
        if (player.State is PlayerState.Matchmaking && leader.State is PlayerState.Matchmaking)
        {
            _matchmakingService.LeaveMatchmaking(player);
        }

        if (player.State is PlayerState.Queued && leader.State is PlayerState.Queued)
        {
            _queueingService.LeaveQueue(player);
        }
    }

    private async Task<bool> LeaveOrCloseParty(Player player)
    {
        if (player.Party is not Party party)
        {
            return false;
        }

        string partyGroupName = GetPartyGroupName(party);

        if (player.IsPartyLeader)
        {
            // close party
            if (Parties.TryRemove(party.Id, out _))
            {
                IReadOnlyList<Player> removedPlayers = party.CloseParty();

                _logger.LogInformation("Closed party {partyId} with {numMembers}", party.Id, removedPlayers.Count);

                await Clients.OthersInGroup(partyGroupName).OnPartyClosed();

                foreach (Player removedPlayer in removedPlayers)
                {
                    if (removedPlayer.PartyHubId is not null)
                    {
                        await Groups.RemoveFromGroupAsync(removedPlayer.PartyHubId, partyGroupName);
                    }
                }

                OnRemovedFromParty(player, player);
            }
        }
        else
        {
            // remove user from party
            if (party.RemovePlayer(player))
            {
                await Clients.OthersInGroup(partyGroupName).OnUserLeftParty(player.Id);
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, partyGroupName);

            OnRemovedFromParty(player, party.Leader);
        }

        return true;
    }

    public Task<bool> LeaveParty()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult(false);
        }

        return LeaveOrCloseParty(player);
    }

    public async Task JoinServer(SimpleServerInfo server)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return;
        }

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
        await Clients.OthersInGroup(GetPartyGroupName(player.Party)).OnServerChanged(server);
    }

    public async Task UpdatePlayerName(string newName)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return;
        }

        if (player.Name.Equals(newName, StringComparison.Ordinal))
        {
            return;
        }

        string oldName = player.Name;
        player.Name = newName;
        _logger.LogInformation("Player name changed from '{oldName}' to '{newName}' for {player}", oldName, newName, player);

        if (player.Party is not null)
        {
            await Clients.OthersInGroup(GetPartyGroupName(player.Party)).OnUserNameChanged(player.Id, newName);
        }
    }

    public async Task<bool> KickPlayer(string id)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return false;
        }

        if (player.Id == id)
        {
            // cannot kick self
            return false;
        }

        if (!player.IsPartyLeader)
        {
            // not a party leader
            return false;
        }

        Party party = player.Party;
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
            await Clients.GroupExcept(partyGroupName, memberToRemove.PartyHubId!)
                .OnUserLeftParty(memberToRemove.Id);

            // notify user that he was kicked
            await Clients.Client(memberToRemove.PartyHubId!).OnKickedFromParty();
        }

        await Groups.RemoveFromGroupAsync(memberToRemove.PartyHubId!, partyGroupName);

        OnRemovedFromParty(memberToRemove, player);

        return true;
    }

    public override async Task OnConnectedAsync()
    {
        string uniqueId = Context.UserIdentifier!;
        string playerName = Context.User!.Identity!.Name!;

        Player player = await _playerStore.GetOrAdd(uniqueId, Context.ConnectionId, playerName);

        if (player.PartyHubId is not null)
        {
            // Reject the connection because the user is already connected
            await Clients.Caller.OnConnectionRejected("Already connected");
            Context.Abort();
            return;
        }

        player.PartyHubId = Context.ConnectionId;

        ConnectedPlayers[Context.ConnectionId] = player;

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Player? player = await _playerStore.TryRemove(Context.UserIdentifier!, Context.ConnectionId);

        if (player is not null)
        {
            _logger.LogDebug("Player {player} disconnected from party hub, dissolving party...", player);

            player.PartyHubId = null;
            await LeaveOrCloseParty(player);
        }

        ConnectedPlayers.TryRemove(Context.ConnectionId, out _);

        await base.OnDisconnectedAsync(exception);
    }
}
