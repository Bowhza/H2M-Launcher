using System.Security;

using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Joining;

/// <summary>
/// Service that handles the whole server joining process.
/// </summary>
public abstract class ServerJoinServiceBase : IServerJoinService, IRecipient<JoinRequestMessage>
{
    private readonly IOptionsMonitor<H2MLauncherSettings> _options;
    private readonly H2MCommunicationService _h2mCommunicationService;
    private readonly QueueingService _queueingService;

    private volatile int _isJoining;
    private SecureString? _lastServerPassword;

    public ServerJoinServiceBase(
        IOptionsMonitor<H2MLauncherSettings> options,
        H2MCommunicationService h2mCommunicationService,
        QueueingService queueingService)
    {
        _options = options;
        _h2mCommunicationService = h2mCommunicationService;
        _queueingService = queueingService;

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <summary>
    /// Gets the previously joined server info.
    /// </summary>
    public ISimpleServerInfo? LastServer { get; private set; }

    /// <summary>
    /// Whether a server is being joined currently.
    /// </summary>
    public bool IsJoining => _isJoining == 1;

    /// <summary>
    /// Gets the current <see cref="JoinKind"/> while a join is in progress.
    /// </summary>
    protected JoinKind CurrentJoinKind { get; private set; }

    /// <summary>
    /// Raised when a server is joined.
    /// </summary>
    public event Action<ISimpleServerInfo, JoinKind>? ServerJoined;

    protected virtual void OnServerJoined(ISimpleServerInfo server, JoinKind joinKind)
    {
        ServerJoined?.Invoke(server, joinKind);
    }

    /// <summary>
    /// Called when the <paramref name="server"/> is private and requires a password.
    /// </summary>
    /// <returns>The private password or <see langword="null"/> to abort the join.</returns>
    protected virtual ValueTask<string?> OnPasswordRequired(IServerInfo server)
    {
        return ValueTask.FromResult<string?>(null);
    }

    /// <summary>
    /// Called when the <paramref name="server"/> has a missing map.
    /// </summary>
    /// <returns>True to join anyways, otherwise aborts the join with <see cref="JoinServerResult.MissingMap"/>.</returns>
    protected virtual ValueTask<bool> OnMissingMap(IServerInfo server)
    {
        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// Called when the <paramref name="server"/> has no free slots.
    /// The default implementation joins the server queue if queueing is enabled.
    /// </summary>
    /// <param name="password">The already requested server password if the server is private.</param>
    /// <returns>The final join result.</returns>
    protected virtual async ValueTask<JoinServerResult> OnServerFull(IServerInfo server, string? password)
    {
        if (!_options.CurrentValue.ServerQueueing)
        {
            // queueing disabled
            return JoinServerResult.ServerFull;
        }

        bool joinedQueue = await _queueingService.JoinQueueAsync(server, null, password);
        return joinedQueue ? JoinServerResult.QueueJoined : JoinServerResult.QueueUnavailable;
    }

    /// <summary>
    /// Called when the game is not running while joining the <paramref name="server"/>.
    /// Default implementation joins anyways when the game detection is not running.
    /// </summary>
    /// <returns>True to join anyways, otherwise aborts the join with <see cref="JoinServerResult.GameNotRunning"/>.</returns>
    protected virtual ValueTask<bool> OnGameNotRunning(IServerInfo server)
    {
        return ValueTask.FromResult(!_h2mCommunicationService.GameDetection.IsGameDetectionRunning);
    }

    /// <summary>
    /// Initiate the joining process for the <paramref name="server"/> with the <paramref name="joinKind"/>.
    /// </summary>
    /// <param name="server">The server to join.</param>
    /// <param name="joinKind">The kind of join operation.</param>
    /// <returns>A result that indicates the outcome of the join.</returns>
    public async Task<JoinServerResult> JoinServer(IServerInfo server, JoinKind joinKind)
    {
        if (Interlocked.Exchange(ref _isJoining, 1) == 1)
        {
            return JoinServerResult.AlreadyJoining;
        }

        CurrentJoinKind = joinKind;

        try
        {
            if (_h2mCommunicationService.GameDetection.DetectedGame is null &&
                !await OnGameNotRunning(server))
            {
                return JoinServerResult.GameNotRunning;
            }

            if (!server.HasMap && !await OnMissingMap(server))
            {
                return JoinServerResult.MissingMap;
            }

            string? password = null;
            if (server.IsPrivate)
            {
                password = await OnPasswordRequired(server);
                if (password is null)
                {
                    return JoinServerResult.NoPassword;
                }
            }

            int privilegedSlots = server.PrivilegedSlots < 0 ? 0 : server.PrivilegedSlots;
            int assumedMaxClients = server.MaxClients - privilegedSlots;
            if (server.RealPlayerCount >= assumedMaxClients)
            {
                // server is full (TODO: check again if refresh was long ago to avoid unnecessary server communication?)

                return await OnServerFull(server, password);
            }

            bool joinedSuccessfully = await TryJoinServer(server, password);
            return joinedSuccessfully ? JoinServerResult.Success : JoinServerResult.JoinFailed;
        }
        finally
        {
            _isJoining = 0;
        }
    }

    /// <summary>
    /// Tries to join the <paramref name="server"/> with the given <paramref name="password"/> and <paramref name="joinKind"/>.
    /// Skips all validation and tries to join directly.
    /// </summary>
    /// <param name="server">The server to join.</param>
    /// <param name="password">The password for the server.</param>
    /// <param name="joinKind">The kind of join operation.</param>
    /// <returns>A result that indicates the outcome of the join.</returns>
    public async Task<JoinServerResult> JoinServer(ISimpleServerInfo server, string? password, JoinKind joinKind)
    {
        if (Interlocked.Exchange(ref _isJoining, 1) == 1)
        {
            return JoinServerResult.AlreadyJoining;
        }

        CurrentJoinKind = joinKind;

        try
        {
            bool joinedSuccessfully = await TryJoinServer(server, password);
            return joinedSuccessfully ? JoinServerResult.Success : JoinServerResult.JoinFailed;
        }
        finally
        {
            _isJoining = 0;
        }
    }

    /// <summary>
    /// Tries to join the <see cref="LastServer"/> (if any).
    /// Skips all validation and tries to join directly.
    /// </summary>
    /// <returns>A result that indicates the outcome of the join.</returns>
    public Task<JoinServerResult> JoinLastServer()
    {
        if (LastServer is null)
        {
            return Task.FromResult(JoinServerResult.None);
        }

        return JoinServer(LastServer, _lastServerPassword?.ToUnsecuredString(), JoinKind.Rejoin);
    }

    /// <summary>
    /// Tries to join the <paramref name="server"/> directly with the given <paramref name="password"/>.
    /// </summary>
    /// <returns>True if the join was successfully triggered.</returns>
    protected async Task<bool> TryJoinServer(ISimpleServerInfo server, string? password)
    {
        bool hasJoined = await _h2mCommunicationService.JoinServer(server.Ip, server.Port.ToString(), password);
        if (hasJoined)
        {
            LastServer = server;
            _lastServerPassword = password?.ToSecuredString();

            OnServerJoined(server, CurrentJoinKind);
        }

        return hasJoined;
    }

    /// <summary>
    /// Process external join requests from other services.
    /// </summary>
    void IRecipient<JoinRequestMessage>.Receive(JoinRequestMessage message)
    {
        message.Reply(JoinServer(message.Server, message.Password, message.Kind));
    }
}
