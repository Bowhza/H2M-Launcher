using System.Collections.Concurrent;

using MatchmakingServer.Core.Social;
using MatchmakingServer.Database.Entities;
using MatchmakingServer.Social;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SocialHub : Hub<ISocialClient>, ISocialHub
{
    private static readonly ConcurrentDictionary<string, Player> ConnectedPlayers = [];

    private readonly FriendshipsService _friendshipsService;
    private readonly UserManager _userManager;
    private readonly PlayerStore _playerStore;
    private readonly ILogger<SocialHub> _logger;

    public SocialHub(PlayerStore playerStore, UserManager userManager, FriendshipsService friendshipsService, ILogger<SocialHub> logger)
    {
        _playerStore = playerStore;
        _userManager = userManager;
        _friendshipsService = friendshipsService;
        _logger = logger;
    }

    public async Task UpdateGameStatus(GameStatus gameStatus)
    {
        string userId = Context.UserIdentifier!;

        _logger.LogDebug("Updating game status for {userId} ({socialHubConnectionId}) to {gameStatus}",
            userId, Context.ConnectionId, gameStatus);

        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            _logger.LogWarning("No connected player found for connection id {socialHubConnectionId}", Context.ConnectionId);
            return;
        }

        // update game status in session
        player.GameStatus = gameStatus;

        await TryNotifyOnlineFriendsOfStatusChange(userId, player);
    }

    public async Task UpdatePlayerName(string newPlayerName)
    {
        string userId = Context.UserIdentifier!;

        _logger.LogDebug("Updating player name for {userId} ({socialHubConnectionId}) to {newPlayerName}",
            userId, Context.ConnectionId, newPlayerName);

        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
        {
            _logger.LogWarning("No connected player found for connection id {socialHubConnectionId}", Context.ConnectionId);
            return;
        }

        // update name in session
        player.Name = newPlayerName;

        // update name in database
        await _userManager.UpdatePlayerNameAsync(
            Guid.Parse(userId), newPlayerName, CancellationToken.None);

        await TryNotifyOnlineFriendsOfStatusChange(userId, player);
    }

    private async Task TryNotifyOnlineFriendsOfStatusChange(string userId, Player player)
    {
        try
        {
            // notify (online) friends of the change
            await Clients.Users(await GetOnlineFriendIds())
                .OnFriendStatusChanged(userId, player.Name, player.GameStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying friends of {userId} of status change", userId);
        }
    }

    private async Task<List<Player>> GetOnlineFriends(string? userId = null)
    {
        userId ??= Context.UserIdentifier!;

        _logger.LogDebug("Querying online friends for {userId}", userId);

        List<Player> onlinePlayers = [];
        List<Guid> friendIds = await _friendshipsService.GetFriendIdsAsync(Guid.Parse(userId)).ConfigureAwait(false);
        foreach (Guid friendId in friendIds)
        {
            if (ConnectedPlayers.TryGetValue(friendId.ToString(), out Player? player))
            {
                onlinePlayers.Add(player);
            }
        }

        return onlinePlayers;
    }

    private async Task<List<string>> GetOnlineFriendIds(string? userId = null)
    {
        userId ??= Context.UserIdentifier!;

        List<string> onlineIds = [];
        List<Guid> friendIds = await _friendshipsService.GetFriendIdsAsync(Guid.Parse(userId)).ConfigureAwait(false);
        foreach (Guid friendId in friendIds)
        {
            if (ConnectedPlayers.ContainsKey(friendId.ToString()))
            {
                onlineIds.Add(friendId.ToString());
            }
        }

        return onlineIds;
    }

    public override async Task OnConnectedAsync()
    {
        string userId = Context.UserIdentifier!;
        string playerName = Context.User!.Identity!.Name!;

        Player player = await _playerStore.GetOrAdd(userId, Context.ConnectionId, playerName);

        if (player.SocialHubId is not null)
        {
            // Reject the connection because the user is already connected
            // TODO: We might want to change that if we decided to allow multiple connections per user
            Context.Abort();
            return;
        }
        player.SocialHubId = Context.ConnectionId;

        await Clients.Users(await GetOnlineFriendIds())
            .OnFriendOnline(userId);

        ConnectedPlayers[userId] = player;

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string userId = Context.UserIdentifier!;

        Player? player = await _playerStore.TryRemove(Context.UserIdentifier!, Context.ConnectionId);

        ConnectedPlayers.TryRemove(Context.UserIdentifier!, out _);

        await Clients.Users(await GetOnlineFriendIds())
            .OnFriendOnline(userId);

        await base.OnDisconnectedAsync(exception);
    }
}
