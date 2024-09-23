using System.Security;

using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Joining;

public abstract class ServerJoinServiceBase : IServerJoinService, IRecipient<JoinRequestMessage>
{
    private readonly IOptionsMonitor<H2MLauncherSettings> _options;
    private readonly H2MCommunicationService _h2mCommunicationService;
    private readonly MatchmakingService _matchmakingService;

    private volatile int _isJoining;
    private SecureString? _lastServerPassword;

    public ServerJoinServiceBase(
        IOptionsMonitor<H2MLauncherSettings> options,
        H2MCommunicationService h2mCommunicationService,
        MatchmakingService matchmakingService)
    {
        _options = options;
        _h2mCommunicationService = h2mCommunicationService;
        _matchmakingService = matchmakingService;

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ISimpleServerInfo? LastServer { get; private set; }
    public bool IsJoining => _isJoining == 1;
    protected JoinKind CurrentJoinKind { get; private set; }


    public event Action<ISimpleServerInfo, JoinKind>? ServerJoined;

    protected virtual void OnServerJoined(ISimpleServerInfo server, JoinKind joinKind)
    {
        ServerJoined?.Invoke(server, joinKind);
    }

    protected virtual ValueTask<string?> OnPasswordRequired(IServerInfo server)
    {
        return ValueTask.FromResult<string?>(null);
    }

    protected virtual ValueTask<bool> OnMissingMap(IServerInfo server)
    {
        return ValueTask.FromResult(false);
    }

    protected virtual async ValueTask<JoinServerResult> OnServerFull(IServerInfo server, string? password)
    {
        if (!_options.CurrentValue.ServerQueueing)
        {
            // queueing disabled
            return JoinServerResult.ServerFull;
        }

        bool joinedQueue = await _matchmakingService.JoinQueueAsync(server, null, password);
        return joinedQueue ? JoinServerResult.QueueJoined : JoinServerResult.QueueUnavailable;
    }

    protected virtual ValueTask<bool> OnGameNotRunning(IServerInfo server)
    {
        return ValueTask.FromResult(!_h2mCommunicationService.GameDetection.IsGameDetectionRunning);
    }

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

    public Task<JoinServerResult> JoinLastServer()
    {
        if (LastServer is null)
        {
            return Task.FromResult(JoinServerResult.None);
        }

        return JoinServer(LastServer, _lastServerPassword?.ToUnsecuredString(), JoinKind.Rejoin);
    }

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

    void IRecipient<JoinRequestMessage>.Receive(JoinRequestMessage message)
    {
        message.Reply(JoinServer(message.Server, message.Password, message.Kind));
    }
}
