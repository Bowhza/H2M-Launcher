﻿using System.Net;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Game.Models;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.OnlineServices.Authentication;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities.SignalR;

using MatchmakingServer.Core.Social;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Refit;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Social;

public sealed class SocialClient : HubClient<ISocialHub>, ISocialClient, IDisposable
{
    private readonly IDisposable _clientRegistration;

    private readonly IPlayerNameProvider _playerNameProvider;
    private readonly IGameCommunicationService _gameCommunicationService;
    private readonly IFriendshipApiClient _friendshipApiClient;
    private readonly IOptionsMonitor<H2MLauncherSettings> _settings;

    private readonly ILogger<SocialClient> _logger;
    private readonly ClientContext _clientContext;

    public ClientContext Context => _clientContext;
    public IFriendshipApiClient FriendshipApi => _friendshipApiClient;

    public GameStatus GameStatus { get; private set; }
    public OnlineStatus OnlineStatus { get; private set; }


    private List<FriendDto> _friends = [];
    private List<FriendRequestDto> _friendRequests = [];

    public IReadOnlyList<FriendDto> Friends => _friends.AsReadOnly();
    public IReadOnlyList<FriendRequestDto> FriendRequests => _friendRequests.AsReadOnly();

    public event Action? FriendsChanged;
    public event Action? FriendRequestsChanged;
    public event Action<FriendRequestDto>? FriendRequestReceived;
    public event Action<FriendDto>? FriendChanged;

    /// <summary>
    /// Raised when the <see cref="GameStatus"/> or <see cref="OnlineStatus"/> has changed.
    /// </summary>
    public event Action? StatusChanged;

    public event Action? ConnectionIssue;

    public SocialClient(
        IPlayerNameProvider playerNameProvider,
        IGameCommunicationService gameCommunicationService,
        IOnlineServices onlineService,
        IFriendshipApiClient friendshipApiClient,
        ILogger<SocialClient> logger,
        HubConnection hubConnection,
        IOptionsMonitor<H2MLauncherSettings> settings) : base(hubConnection)
    {
        _clientRegistration = hubConnection.Register<ISocialClient>(this);

        _playerNameProvider = playerNameProvider;
        _gameCommunicationService = gameCommunicationService;
        _clientContext = onlineService.ClientContext;
        _friendshipApiClient = friendshipApiClient;
        _logger = logger;
        _settings = settings;
    }

    protected override ISocialHub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken)
    {
        return hubConnection.CreateHubProxy<ISocialHub>(hubCancellationToken);
    }

    private static GameStatus CreateGameStatus(GameState state)
    {
        return state switch
        {
            { IsInMainMenu: true } => GameStatus.InMainMenu,
            { IsConnected: true, IsPrivateMatch: true } => GameStatus.InPrivateMatch,
            { IsConnected: true } => GameStatus.InMatch,
            { ConnectionState: ConnectionState.CA_DISCONNECTED } => GameStatus.None,
            _ => GameStatus.InMainMenu
        };
    }

    private async void GameCommunicationService_GameStateChanged(GameState state)
    {
        try
        {
            if (Connection.State is HubConnectionState.Disconnected)
            {
                return;
            }

            GameStatus = CreateGameStatus(state);

            await Hub.UpdateGameStatus(GameStatus);

            StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating game status to {newGameStatus}.", GameStatus);
        }
    }

    private async void GameCommunicationService_Stopped(Exception? obj)
    {
        try
        {
            if (Connection.State is HubConnectionState.Disconnected)
            {
                return;
            }

            GameStatus = GameStatus.None;

            await Hub.UpdateGameStatus(GameStatus.None);

            StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating game status to {newGameStatus}.", GameStatus.None);
        }
    }

    private async void PlayerNameProvider_PlayerNameChanged(string oldName, string newName)
    {
        try
        {
            if (Connection.State is HubConnectionState.Disconnected ||
                !_settings.CurrentValue.PublicPlayerName)
            {
                return;
            }

            await Hub.UpdatePlayerName(newName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating player name from {oldName} to {newName}.", oldName, newName);
        }
    }

    private async Task SendCurrentGameStatus()
    {
        try
        {
            if (_gameCommunicationService.GameProcess is not null)
            {
                // send initial game status
                GameStatus = CreateGameStatus(_gameCommunicationService.CurrentGameState);

                await Hub.UpdateGameStatus(GameStatus);

                StatusChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating game status to {newGameStatus}.", GameStatus);
        }
    }

    private async Task FetchFriendsAsync()
    {
        _logger.LogDebug("Fetching friends");

        IApiResponse<List<FriendDto>> response = await _friendshipApiClient.GetFriendsAsync(_clientContext.UserId!);
        if (response.IsSuccessful)
        {
            _friends = response.Content;

            FriendsChanged?.Invoke();

            _logger.LogInformation("Successfully fetched {numFriends} friends", response.Content.Count);
        }
        else
        {
            _logger.LogError(response.Error, "Failed to fetch friends");
        }
    }

    private async Task FetchFriendRequestsAsync()
    {
        _logger.LogDebug("Fetching friend requests");

        IApiResponse<List<FriendRequestDto>> response = await _friendshipApiClient.GetFriendRequestsAsync(_clientContext.UserId!);
        if (response.IsSuccessful)
        {
            _friendRequests = response.Content;

            FriendRequestsChanged?.Invoke();

            _logger.LogInformation("Successfully fetched {numFriendRequests} friend requests", response.Content.Count);
        }
        else
        {
            _logger.LogError(response.Error, "Failed to fetch friend requests");
        }
    }

    public async Task<bool> AddFriendAsync(string friendId)
    {
        IApiResponse<FriendRequestDto> response = await _friendshipApiClient.SendFriendRequestAsync(
            _clientContext.UserId!, new(Guid.Parse(friendId)));

        if (response.IsSuccessful)
        {
            _logger.LogDebug("Successfully requested friends to {friendId}", friendId);

            _friendRequests.Add(response.Content);
            FriendRequestsChanged?.Invoke();

            return true;
        }

        _logger.LogError(response.Error, "Error requested friends to {friendId}", friendId);
        return false;
    }

    public async Task<bool> RemoveFriendAsync(string friendId)
    {
        IApiResponse response = await _friendshipApiClient.UnfriendAsync(
            _clientContext.UserId!, friendId);

        if (response.IsSuccessful)
        {
            _logger.LogDebug("Successfully unfriended {friendId}", friendId);

            _friends.RemoveAll(f => f.Id == friendId);
            FriendsChanged?.Invoke();

            return true;
        }

        _logger.LogError(response.Error, "Error unfriending {friendId}", friendId);
        return false;
    }

    public async Task<bool> AcceptFriendAsync(string friendId)
    {
        var response = await _friendshipApiClient.AcceptFriendRequestAsync(_clientContext.UserId!, friendId);
        if (response.IsSuccessful)
        {
            _logger.LogDebug("Accepted incoming friend request of {friendId}", friendId);

            _friendRequests.RemoveAll(fr => fr.UserId.ToString() == friendId);
            _friends.Add(response.Content);

            FriendsChanged?.Invoke();
            FriendRequestsChanged?.Invoke();

            return true;
        }
        else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            // friend request was probably invalid, so remove
            _friendRequests.RemoveAll(fr => fr.UserId.ToString() == friendId);

            FriendRequestsChanged?.Invoke();

            return true;
        }

        _logger.LogError(response.Error, "Error accepting friend {friendId}", friendId);
        return false;
    }

    public async Task<bool> RejectFriendAsync(string friendId)
    {
        var response = await _friendshipApiClient.RejectFriendRequestAsync(_clientContext.UserId!, friendId);
        if (response.IsSuccessful)
        {
            _logger.LogDebug("Rejected incoming friend request of {friendId}", friendId);

            _friendRequests.RemoveAll(fr => fr.UserId.ToString() == friendId);            

            FriendsChanged?.Invoke();
            return true;
        }
        else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            // friend request was probably invalid, so remove
            _friendRequests.RemoveAll(fr => fr.UserId.ToString() == friendId);

            FriendRequestsChanged?.Invoke();

            return true;
        }

        _logger.LogError(response.Error, "Error rejecting friend {friendId}", friendId);
        return false;
    }


    protected override async Task OnConnected(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Social client connected.");

        _playerNameProvider.PlayerNameChanged += PlayerNameProvider_PlayerNameChanged;
        _gameCommunicationService.GameStateChanged += GameCommunicationService_GameStateChanged;
        _gameCommunicationService.Stopped += GameCommunicationService_Stopped;

        OnlineStatus = OnlineStatus.Online;

        await SendCurrentGameStatus();
        await FetchFriendsAsync();
        await FetchFriendRequestsAsync();

        await base.OnConnected(cancellationToken);
    }

    protected override Task OnReconnecting(Exception? exception)
    {
        _logger.LogDebug(exception, "Social client reconnecting {state}", Connection.State);

        // The connection has been lost, so we set everything to offline
        SetOffline();

        // Notify that we are experiencing connection problems
        ConnectionIssue?.Invoke();

        return base.OnReconnecting(exception);
    }

    protected override async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("Social client reconnected.");

        OnlineStatus = OnlineStatus.Online;

        await SendCurrentGameStatus();
        await FetchFriendsAsync();
        await FetchFriendRequestsAsync();

        await base.OnReconnected(connectionId);
    }

    private void SetOffline()
    {
        // set self to offline
        OnlineStatus = OnlineStatus.Online;
        GameStatus = GameStatus.None;

        StatusChanged?.Invoke();

        // set all friends to offline
        for (int i = 0; i < _friends.Count; i++)
        {
            _friends[i] = _friends[i] with
            {
                Status = OnlineStatus.Offline,
                GameStatus = GameStatus.None,
                PartyStatus = null
            };
        }
    }

    protected override Task OnConnectionClosed(Exception? exception)
    {
        _logger.LogInformation(exception, "Social client connection closed.");

        SetOffline();

        _playerNameProvider.PlayerNameChanged -= PlayerNameProvider_PlayerNameChanged;
        _gameCommunicationService.GameStateChanged -= GameCommunicationService_GameStateChanged;
        _gameCommunicationService.Stopped -= GameCommunicationService_Stopped;

        return base.OnConnectionClosed(exception);
    }

    #region RPC Handlers


    Task ISocialClient.OnFriendOnline(string friendId, string playerName)
    {
        FriendDto? newFriend = UpdateFriend(friendId, (friend) => friend with
        {
            PlayerName = playerName,
            Status = OnlineStatus.Online
        });

        if (newFriend is not null)
        {
            FriendsChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    Task ISocialClient.OnFriendOffline(string friendId)
    {
        FriendDto? newFriend = UpdateFriend(friendId, (friend) => friend with
        {
            Status = OnlineStatus.Offline,
            GameStatus = GameStatus.None,
            PartyStatus = null
        });

        if (newFriend is not null)
        {
            FriendsChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    Task ISocialClient.OnFriendStatusChanged(string friendId, string playerName, GameStatus status, PartyStatusDto? partyStatus)
    {
        FriendDto? newFriend = UpdateFriend(friendId, (friend) => friend with
        {
            PlayerName = playerName,
            GameStatus = status,
            PartyStatus = partyStatus,
        });

        return Task.CompletedTask;
    }

    Task ISocialClient.OnFriendRequestAccepted(FriendDto newFriend)
    {
        _logger.LogDebug("{friendId} accepted the friend request", newFriend.Id);

        _friendRequests.RemoveAll(fr => fr.UserId.ToString() == newFriend.Id);
        _friends.Add(newFriend);

        FriendRequestsChanged?.Invoke();
        FriendsChanged?.Invoke();

        return Task.CompletedTask;
    }

    Task ISocialClient.OnFriendRequestReceived(FriendRequestDto request)
    {
        _logger.LogDebug("Received friend request from {friendId}", request.UserId);

        _friendRequests.Add(request);
        FriendRequestReceived?.Invoke(request);
        FriendRequestsChanged?.Invoke();

        return Task.CompletedTask;
    }

    Task ISocialClient.OnUnfriended(string byFriendId)
    {
        FriendDto? friend = _friends.Find(f => f.Id == byFriendId);
        if (friend is null)
        {
            _logger.LogWarning("Cannot find friend with id {userId}", byFriendId);
            return Task.CompletedTask;
        }

        if (_friends.Remove(friend))
        {
            FriendsChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    #endregion

    private FriendDto? UpdateFriend(string friendId, Func<FriendDto, FriendDto> updateFunc)
    {
        ArgumentNullException.ThrowIfNull(_friends);

        int friendIndex = _friends.FindIndex(f => f.Id == friendId);
        if (friendIndex == -1)
        {
            _logger.LogWarning("Cannot find friend with id {userId}", friendId);
            return null;
        }

        FriendDto friend = _friends[friendIndex];
        FriendDto updatedFriend = updateFunc(friend);

        _friends[friendIndex] = updatedFriend;

        if (updatedFriend is not null)
        {
            FriendChanged?.Invoke(updatedFriend);
        }

        return updatedFriend;
    }

    public Task OnClosed(Exception? exception)
    {
        _logger.LogDebug(exception, "Party hub connection closed");

        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clientRegistration.Dispose();
            _playerNameProvider.PlayerNameChanged -= PlayerNameProvider_PlayerNameChanged;
            _gameCommunicationService.GameStateChanged -= GameCommunicationService_GameStateChanged;
            _gameCommunicationService.Stopped -= GameCommunicationService_Stopped;
        }

        base.Dispose(disposing);
    }
}
