using MatchmakingServer.Core.Social;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SocialHub : Hub<ISocialClient>, ISocialHub
{
    private readonly PlayerStore _playerStore;
    private readonly SocialService _socialService;
    private readonly ILogger<SocialHub> _logger;

    public SocialHub(ILogger<SocialHub> logger, PlayerStore playerStore, SocialService socialService)
    {
        _logger = logger;
        _playerStore = playerStore;
        _socialService = socialService;
    }

    public Task UpdateGameStatus(GameStatus gameStatus)
    {
        if (string.IsNullOrEmpty(Context.UserIdentifier))
        {
            return Task.CompletedTask;
        }

        return _socialService.UpdateGameStatus(Context.UserIdentifier, Context.ConnectionId, gameStatus);
    }

    public Task UpdatePlayerName(string newPlayerName)
    {
        if (string.IsNullOrEmpty(Context.UserIdentifier))
        {
            return Task.CompletedTask;
        }

        return _socialService.UpdatePlayerName(Context.UserIdentifier, Context.ConnectionId, newPlayerName);
    }

    public override async Task OnConnectedAsync()
    {
        string userId = Context.UserIdentifier!;
        string userName = Context.User!.Identity!.Name!;
        string? playerName = Context.GetHttpContext()?.Request.Query["playerName"].SingleOrDefault();

        Player player = await _playerStore.GetOrAdd(userId, Context.ConnectionId, playerName ?? userName);

        if (player.SocialHubId is not null)
        {
            // Reject the connection because the user is already connected
            // TODO: We might want to change that if we decided to allow multiple connections per user
            Context.Abort();
            return;
        }
        player.SocialHubId = Context.ConnectionId;

        if (!string.IsNullOrEmpty(playerName))
        {
            player.Name = playerName;
        }

        await _socialService.OnPlayerOnline(player);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string userId = Context.UserIdentifier!;

        Player? player = await _playerStore.TryRemove(userId, Context.ConnectionId);
        if (player is not null)
        {
            player.SocialHubId = null;
        }

        await _socialService.OnPlayerOffline(userId);

        await base.OnDisconnectedAsync(exception);
    }
}
