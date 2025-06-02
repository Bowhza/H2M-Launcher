using Flurl;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.OnlineServices.Authentication;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities.SignalR;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.OnlineServices;

/// <summary>
/// Manages connection to online services (such as party or matchmaking) with SignalR.
/// </summary>
public sealed class OnlineServiceManager : IOnlineServices, IAsyncDisposable
{
    private readonly ILogger<OnlineServiceManager> _logger;
    private readonly IOptions<MatchmakingSettings> _options;
    private readonly AuthenticationService _authenticationService;
    private readonly SemaphoreSlim _authenticationLock = new(1, 1);

    private readonly List<CustomHubConnection> _hubConnections = [];

    public ClientContext ClientContext { get; }

    public HubConnection QueueingHubConnection { get; }
    public HubConnection PartyHubConnection { get; }

    public HubConnection SocialHubConnection { get; }

    public bool IsPartyServiceConnected => PartyHubConnection.State is HubConnectionState.Connected;
    public bool IsQueueingServiceConnected => QueueingHubConnection.State is HubConnectionState.Connected;
    public bool IsSocialServiceConnected => QueueingHubConnection.State is HubConnectionState.Connected;


    private PlayerState _state = PlayerState.Disconnected;
    public PlayerState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _logger.LogTrace("Set client state to {state}", value);

            PlayerState oldState = _state;
            _state = value;

            OnStateChanged(oldState, value);
        }
    }

    public event Action<PlayerState, PlayerState>? StateChanged;

    public OnlineServiceManager(
        ILogger<OnlineServiceManager> logger,
        IOptions<MatchmakingSettings> matchmakingSettings,
        IOptions<H2MLauncherSettings> settings,
        AuthenticationService authenticationService,
        ClientContext clientContext)
    {
        _logger = logger;
        _options = matchmakingSettings;
        _authenticationService = authenticationService;
        ClientContext = clientContext;

        object queryParams = settings.Value.PublicPlayerName ? new
        {
            playerName = ClientContext.PlayerName
        } : new { };

        QueueingHubConnection = new CustomHubConnectionBuilder()
            .WithUrl(matchmakingSettings.Value.QueueingHubUrl.SetQueryParams(queryParams), (opts) =>
            {
                opts.AccessTokenProvider = GetAccessTokenAsync;
                opts.HttpMessageHandlerFactory = CreateMessageHandler;

                // add headers to identify app version
                AddAppHeaders(opts.Headers);
            })
            .Build();

        PartyHubConnection = new CustomHubConnectionBuilder()
            .WithUrl(matchmakingSettings.Value.PartyHubUrl.SetQueryParams(queryParams), (opts) =>
            {
                opts.AccessTokenProvider = GetAccessTokenAsync;
                opts.HttpMessageHandlerFactory = CreateMessageHandler;

                AddAppHeaders(opts.Headers);
            })
            .WithAutomaticReconnect()
            .Build();

        SocialHubConnection = new CustomHubConnectionBuilder()
            .WithUrl(matchmakingSettings.Value.SocialHubUrl.SetQueryParams(queryParams), (opts) =>
            {
                opts.AccessTokenProvider = GetAccessTokenAsync;
                opts.HttpMessageHandlerFactory = CreateMessageHandler;

                AddAppHeaders(opts.Headers);
            })
            .WithAutomaticReconnect(new PartyHubConnectionRetryPolicy())
            .Build();

        _hubConnections = [(CustomHubConnection)QueueingHubConnection, (CustomHubConnection)PartyHubConnection];
        _hubConnections.ForEach(conn => conn.Closed += HubConnection_Closed);
        _hubConnections.ForEach(conn => conn.Connected += HubConnection_Connected);
    }

    private static void AddAppHeaders(IDictionary<string, string> headers)
    {
        // add headers to identify app version
        headers.Add("X-App-Name", "H2MLauncher");
        headers.Add("X-App-Version", LauncherService.CurrentVersion);
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        // Use a lock to prevent multiple hubs from logging in at the same time, leading to errors
        await _authenticationLock.WaitAsync();
        try
        {
            if (ClientContext.IsAuthenticated)
            {
                // stored token still valid
                return ClientContext.AccessToken;
            }

            return await _authenticationService.LoginAsync();
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    private HttpMessageHandler CreateMessageHandler(HttpMessageHandler original)
    {
        return _options.Value.DisableCertificateValidation
            ? new SocketsHttpHandler()
            {
                SslOptions = new()
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                }
            }
            : original;
}

    private void OnStateChanged(PlayerState oldState, PlayerState newState)
    {
        StateChanged?.Invoke(oldState, newState);
    }

    private Task HubConnection_Connected(string? connectionId)
    {
        _logger.LogDebug("Hub connection connect with connection id {connectionId}", connectionId);

        if (State is PlayerState.Disconnected)
        {
            State = PlayerState.Connected;
        }

        return Task.CompletedTask;
    }

    private Task HubConnection_Closed(Exception? exception)
    {
        _logger.LogDebug(exception, "Hub connection was closed");

        if (_hubConnections.TrueForAll(conn => conn.State is HubConnectionState.Disconnected))
        {
            State = PlayerState.Disconnected;
        }

        return Task.CompletedTask;
    }

    public async Task StartAllConnections(CancellationToken cancellationToken = default)
    {
        foreach (HubConnection connection in _hubConnections)
        {
            if (connection.State is not HubConnectionState.Disconnected)
            {
                return;
            }

            await connection.StartAsync(cancellationToken);
        }
    }

    public async Task CloseAllConnections(CancellationToken cancellationToken = default)
    {
        foreach (HubConnection connection in _hubConnections)
        {
            if (connection.State is HubConnectionState.Disconnected)
            {
                return;
            }

            await connection.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (HubConnection connection in _hubConnections)
        {
            await connection.DisposeAsync();
        }
    }

    class PartyHubConnectionRetryPolicy : IRetryPolicy
    {
        internal static TimeSpan?[] DEFAULT_RETRY_DELAYS =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
        ];

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return DEFAULT_RETRY_DELAYS[Math.Min(retryContext.PreviousRetryCount, DEFAULT_RETRY_DELAYS.Length - 1)];
        }
    }
}
