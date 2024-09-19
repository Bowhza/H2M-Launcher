using System.Collections.Concurrent;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR;

[Authorize(AuthenticationSchemes = "client")]
class PartyHub : Hub<IPartyClient>, IPartyHub
{
    private static readonly ConcurrentDictionary<string, Party> Parties = [];
    private static readonly ConcurrentDictionary<string, Player> ConnectedPlayers = [];

    private readonly PlayerStore _playerStore;
    private readonly ILogger<PartyHub> _logger;

    public PartyHub(PlayerStore playerStore, ILogger<PartyHub> logger)
    {
        _playerStore = playerStore;
        _logger = logger;
    }

    public async Task<string?> CreateParty()
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
        await Groups.AddToGroupAsync(Context.ConnectionId, $"party_{party.Id}");

        return party.Id;
    }

    public async Task<IReadOnlyList<PartyPlayerInfo>?> JoinParty(string partyId)
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
        await Groups.AddToGroupAsync(Context.ConnectionId, $"party_{party.Id}");

        // notify others of join
        await Clients.OthersInGroup($"party_{party.Id}").OnUserJoinedParty(player.Id, player.Name);

        return party.Members.Select(m => new PartyPlayerInfo(m.Id, m.Name, m.IsPartyLeader)).ToList();
    }

    private async Task<bool> LeaveOrCloseParty(Player player)
    {
        if (player.Party is not Party party)
        {
            return false;
        }

        if (player.IsPartyLeader)
        {
            // close party
            if (Parties.TryRemove(party.Id, out _))
            {
                await Clients.Group($"party_{party.Id}").OnPartyClosed();
            }
        }
        else
        {
            // remove user from party
            if (party.RemovePlayer(player))
            {
                await Clients.Group($"party_{party.Id}").OnUserLeftParty(player.Id, player.Name);
            }
        }

        await Groups.RemoveFromGroupAsync(player.QueueingHubId, $"party_{party.Id}");
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

    public async Task JoinServer(ServerConnectionDetails server)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return;
        }

        if (!player.IsPartyLeader)
        {
            return;
        }

        // TODO: leave party when joined alone and not leader?

        // notify others to join the server
        await Clients.OthersInGroup($"party_{player.Party.Id}").OnJoinServer(server);
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
            await Clients.OthersInGroup($"party_{player.Party.Id}").OnUserNameChanged(player.Id, newName);
        }
    }

    public override async Task OnConnectedAsync()
    {
        string uniqueId = Context.UserIdentifier!;
        string playerName = Context.User!.Identity!.Name!;

        Player player = _playerStore.ConnectedPlayers.GetOrAdd(uniqueId, (id) => new()
        {
            Id = id,
            Name = playerName,
            State = PlayerState.Connected
        });

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
        if (_playerStore.ConnectedPlayers.TryGetValue(Context.UserIdentifier!, out Player? player)
            && player.Party is not null)
        {
            player.PartyHubId = null;
            await LeaveOrCloseParty(player);
        }

        ConnectedPlayers.TryRemove(Context.ConnectionId, out _);

        await base.OnDisconnectedAsync(exception);
    }
}
