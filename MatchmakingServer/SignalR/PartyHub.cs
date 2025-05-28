using System.Collections.Concurrent;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;

using MatchmakingServer.Core.Party;
using MatchmakingServer.Parties;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PartyHub : Hub<IPartyClient>, IPartyHub
{
    private static readonly ConcurrentDictionary<string, Player> ConnectedPlayers = [];

    private readonly PlayerStore _playerStore;
    private readonly PartyService _partyService;
    private readonly ILogger<PartyHub> _logger;

    public PartyHub(PlayerStore playerStore, PartyService partyService, ILogger<PartyHub> logger)
    {
        _playerStore = playerStore;
        _partyService = partyService;
        _logger = logger;
    }

    public Task<PartyInfo?> CreateParty()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult<PartyInfo?>(null);
        }

        return _partyService.CreateParty(player);
    }

    public Task<PartyInfo?> JoinParty(string partyId)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult<PartyInfo?>(null);
        }

        return _partyService.JoinParty(player, partyId);
    }

    public Task JoinServer(SimpleServerInfo server)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.CompletedTask;
        }

        return _partyService.JoinServer(player, server);
    }

    public Task<bool> KickPlayer(string id)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult(false);
        }

        return _partyService.KickPlayer(player, id);
    }

    public Task<bool> LeaveParty()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult(false);
        }

        return _partyService.LeaveOrCloseParty(player);
    }

    public Task<bool> PromoteLeader(string id)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult(false);
        }

        return _partyService.ChangeLeader(player, id);
    }

    public Task<bool> ChangePartyPrivacy(PartyPrivacy newPartyPrivacy)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.FromResult(false);
        }

        return _partyService.ChangePartyPrivacy(player, newPartyPrivacy);
    }

    public Task UpdatePlayerName(string newName)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            return Task.CompletedTask;
        }

        return _partyService.UpdatePlayerName(player, newName);
    }

    public override async Task OnConnectedAsync()
    {
        string uniqueId = Context.UserIdentifier!;
        string userName = Context.User!.Identity!.Name!;
        string? playerName = Context.GetHttpContext()?.Request.Query["playerName"].SingleOrDefault();

        Player player = await _playerStore.GetOrAdd(uniqueId, Context.ConnectionId, playerName ?? userName);

        if (player.PartyHubId is not null)
        {
            // Reject the connection because the user is already connected
            await Clients.Caller.OnConnectionRejected("Already connected");
            Context.Abort();
            return;
        }

        player.PartyHubId = Context.ConnectionId;
        ConnectedPlayers[Context.ConnectionId] = player;

        if (!string.IsNullOrEmpty(playerName))
        {
            player.Name = playerName;
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Player? player = await _playerStore.TryRemove(Context.UserIdentifier!, Context.ConnectionId);

        if (player is not null)
        {
            _logger.LogDebug("Player {player} disconnected from party hub, dissolving party...", player);

            await _partyService.LeaveOrCloseParty(player);
            player.PartyHubId = null;
        }

        ConnectedPlayers.TryRemove(Context.ConnectionId, out _);

        await base.OnDisconnectedAsync(exception);
    }
}
