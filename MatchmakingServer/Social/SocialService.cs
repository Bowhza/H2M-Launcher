using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;

using MatchmakingServer.Parties;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.Social;

public class SocialService
{
    private readonly ConcurrentDictionary<string, Player> _onlinePlayers = [];
    private readonly Subject<IReadOnlyCollection<Player>> _playerStatusChanges = new();
    private readonly Subject<(Player Player, ConnectedServerInfo? ConnectedServer)> _playerConnectedServerChanges = new();

    private readonly PartyService _partyService;
    private readonly IPlayerServerTrackingService _playerServerTrackingService;

    private readonly ILogger<SocialHub> _logger;
    private readonly IHubContext<SocialHub, ISocialClient> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;


    public SocialService(
        PartyService partyService,
        IPlayerServerTrackingService playerServerTrackingService,
        ILogger<SocialHub> logger,
        IHubContext<SocialHub, ISocialClient> hubContext,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceScopeFactory = serviceScopeFactory;

        _playerServerTrackingService = playerServerTrackingService;
        _playerServerTrackingService.PlayerJoinedServer += PlayerServerTrackingService_PlayerJoinedServer;
        _playerServerTrackingService.PlayerLeftServer += PlayerServerTrackingService_PlayerLeftServer;

        _partyService = partyService;
        _partyService.PartyClosed += PartyService_PartyClosed;
        _partyService.PartyCreated += PartyService_PartyCreated;
        _partyService.PlayerJoinedParty += PartyService_PlayerJoinedParty;
        _partyService.PlayerRemovedFromParty += PartyService_PlayerRemovedFromParty;
        _partyService.PartyPrivacyChanged += PartyService_PartyPrivacyChanged;

        // We use a observable pipe to throttle fast subsequent player change notifications (such as leaving and immediately auto-creating a party)
        // so we can minimize noise and database calls.
        _playerStatusChanges
            .SelectMany(players => players)

            // group by player to get a sequence for each player change
            .GroupBy(player => player.Id)

            // debounce each player group, then merge into a single sequence
            .SelectMany(grp => grp.Throttle(TimeSpan.FromSeconds(1)))

            // notify friends of each player
            .SelectMany(player =>
                Observable.FromAsync(async (ct) =>
                {
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();

                    await TryNotifyOnlineFriendsOfStatusChange(player, scope);

                    return player;
                })
            )
            .Subscribe(
                onNext: (p) => _logger.LogTrace("Player status change notification pipe processed {player}", p),
                onError: (ex) => _logger.LogError(ex, "Error in player status change notification pipe")
            );

        // Use another obersvable pipe to throttle handling INCOMING server connection updates to prevent matching during map changes etc.
        _playerConnectedServerChanges
           // group by player to get a sequence for each player change
           .GroupBy(x => x.Player.Id)

           // debounce each player group, then merge into a single sequence
           .SelectMany(grp => grp.Throttle(TimeSpan.FromSeconds(2)))

           // handle server connectino update
           .SelectMany(x =>
               Observable.FromAsync((ct) =>               
                   _playerServerTrackingService.HandlePlayerConnectionUpdate(x.Player, x.ConnectedServer, ct)
               )
           )
           .Subscribe(
               onNext: (_) => { },
               onError: (ex) => _logger.LogError(ex, "Error in server connection update handling pipe")
           );
    }

    public Task UpdateGameStatus(string userId, string connectionId, GameStatus gameStatus, ConnectedServerInfo? connectedServer)
    {
        if (!_onlinePlayers.TryGetValue(userId, out Player? player))
        {
            _logger.LogWarning("No connected player found for user id {userId}", userId);
            return Task.CompletedTask;
        }

        if (player.GameStatus == gameStatus && 
            EqualityComparer<ConnectedServerInfo>.Default.Equals(player.LastConnectedServerInfo, connectedServer))
        {
            // no change
            return Task.CompletedTask;
        }

        _logger.LogTrace("Updating game status for {userId} ({socialHubConnectionId}) to {gameStatus}",
            userId, connectionId, gameStatus);

        // update game status in session
        player.GameStatus = gameStatus;
        player.LastConnectedServerInfo = connectedServer;

        // handle connected server
        _playerConnectedServerChanges.OnNext((player, connectedServer));
        _playerStatusChanges.OnNext([player]);

        return Task.CompletedTask;
    }

    public async Task UpdatePlayerName(string userId, string connectionId, string newPlayerName)
    {
        _logger.LogDebug("Updating player name for {userId} ({socialHubConnectionId}) to {newPlayerName}",
            userId, connectionId, newPlayerName);

        if (!_onlinePlayers.TryGetValue(userId, out Player? player))
        {
            _logger.LogWarning("No connected player found for user id {userId}", userId);
            return;
        }

        if (player.Name == newPlayerName)
        {
            // no change
            return;
        }

        // update name in session
        player.Name = newPlayerName;

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        UserManager userManager = scope.ServiceProvider.GetRequiredService<UserManager>();

        // update name in database
        await userManager.UpdatePlayerNameAsync(
            Guid.Parse(userId), newPlayerName, CancellationToken.None);

        _playerStatusChanges.OnNext([player]);
    }

    public async Task OnPlayerOnline(Player player)
    {
        _onlinePlayers[player.Id] = player;

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        FriendshipsService friendshipsService = scope.ServiceProvider.GetRequiredService<FriendshipsService>();

        await _hubContext.Clients
            .Users(await GetOnlineFriendIds(friendshipsService, player.Id))
            .OnFriendOnline(player.Id, player.Name);

        UserManager userManager = scope.ServiceProvider.GetRequiredService<UserManager>();

        // update player name in database
        await userManager.UpdatePlayerNameAsync(
            Guid.Parse(player.Id), player.Name, CancellationToken.None);
    }

    public async Task OnPlayerOffline(string userId)
    {
        if (_onlinePlayers.TryRemove(userId, out Player? player))
        {
            player.GameStatus = GameStatus.None;
            await _playerServerTrackingService.RemovePlayerFromCurrentServer(player);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            FriendshipsService friendshipsService = scope.ServiceProvider.GetRequiredService<FriendshipsService>();

            await _hubContext.Clients
                .Users(await GetOnlineFriendIds(friendshipsService, player.Id))
                .OnFriendOffline(player.Id);
        }
    }

    private async Task TryNotifyOnlineFriendsOfStatusChange(Player player, AsyncServiceScope serviceScope)
    {
        try
        {
            FriendshipsService friendshipsService = serviceScope.ServiceProvider.GetRequiredService<FriendshipsService>();

            // notify (online) friends of the change
            await _hubContext.Clients.Users(await GetOnlineFriendIds(friendshipsService, player.Id))
                .OnFriendStatusChanged(
                    player.Id,
                    player.Name,
                    player.GameStatus,
                    player.Party is not null
                        ? new PartyStatusDto(
                            player.Party.Id,
                            player.Party.Members.Count,
                            player.Party.Privacy is not PartyPrivacy.Closed,
                            player.Party.ValidInvites.ToList())
                        : null,
                    player.PlayingServer is not null
                        ? new MatchStatusDto(
                            (player.PlayingServer.ServerIp, player.PlayingServer.ServerPort),
                            player.PlayingServer.LastServerInfo?.HostName ?? player.PlayingServer.ServerName,
                            player.PlayingServer.LastServerInfo?.GameType,
                            player.PlayingServer.LastServerInfo?.MapName,
                            player.PlayingServer.KnownPlayers[player])
                        : null
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying friends of {userId} of status change", player.Id);
        }
    }

    private async Task<List<Player>> GetOnlineFriends(FriendshipsService friendshipsService, string userId)
    {
        _logger.LogDebug("Querying online friends for {userId}", userId);

        List<Player> onlinePlayers = [];
        List<Guid> friendIds = await friendshipsService.GetFriendIdsAsync(Guid.Parse(userId)).ConfigureAwait(false);
        foreach (Guid friendId in friendIds)
        {
            if (_onlinePlayers.TryGetValue(friendId.ToString(), out Player? player))
            {
                onlinePlayers.Add(player);
            }
        }

        return onlinePlayers;
    }

    private async Task<List<string>> GetOnlineFriendIds(FriendshipsService friendshipsService, string userId)
    {
        List<string> onlineIds = [];
        List<Guid> friendIds = await friendshipsService.GetFriendIdsAsync(Guid.Parse(userId)).ConfigureAwait(false);
        foreach (Guid friendId in friendIds)
        {
            if (_onlinePlayers.ContainsKey(friendId.ToString()))
            {
                onlineIds.Add(friendId.ToString());
            }
        }

        return onlineIds;
    }

    private void PartyService_PlayerRemovedFromParty(Party party, Player player)
    {
        _playerStatusChanges.OnNext(party.Members.ToList());
    }

    private void PartyService_PlayerJoinedParty(Party party, Player joinedPlayer)
    {
        _playerStatusChanges.OnNext(party.Members.ToList());
    }

    private void PartyService_PartyCreated(Party party)
    {
        _playerStatusChanges.OnNext(party.Members.ToList());
    }

    private void PartyService_PartyClosed(Party party, IReadOnlyCollection<Player> removedPlayers)
    {
        _playerStatusChanges.OnNext(removedPlayers);
    }

    private void PartyService_PartyPrivacyChanged(Party party, PartyPrivacy partyPrivacy)
    {
        _playerStatusChanges.OnNext(party.Members.ToList());
    }

    private void PlayerServerTrackingService_PlayerJoinedServer(Player player, GameServer server)
    {
        _playerStatusChanges.OnNext([player]);
    }

    private void PlayerServerTrackingService_PlayerLeftServer(PlayerServerTrackingService.PlayerLeftEventArgs e)
    {
        if (e.Player.SocialHubId is null)
        {
            // No status change needs to be sent (OnPlayerOffline takes care of that).
            // We are probably here because the player was removed from the server after disconnecting.
            return;
        }

        _playerStatusChanges.OnNext([e.Player]);
    }
}
