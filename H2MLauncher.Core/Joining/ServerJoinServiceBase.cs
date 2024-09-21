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

    private SecureString? _lastServerPassword;

    public ServerJoinServiceBase(
        IOptionsMonitor<H2MLauncherSettings> options,
        H2MCommunicationService h2mCommunicationService,
        MatchmakingService matchmakingService)
    {
        _options = options;
        _h2mCommunicationService = h2mCommunicationService;
        WeakReferenceMessenger.Default.RegisterAll(this);
        _matchmakingService = matchmakingService;
    }

    public IServerConnectionDetails? LastServer { get; private set; }


    public event Action<IServerConnectionDetails>? ServerJoined;

    protected virtual void OnServerJoined(IServerConnectionDetails server)
    {
        ServerJoined?.Invoke(server);
    }

    protected virtual ValueTask<string?> OnPasswordRequired(IServerInfo server)
    {
        return ValueTask.FromResult<string?>(null);
    }

    protected virtual ValueTask<bool> OnMissingMap(IServerInfo server)
    {
        return ValueTask.FromResult(false);
    }

    protected virtual async ValueTask<ServerJoinResult> OnServerFull(IServerInfo server, string? password)
    {
        if (!_options.CurrentValue.ServerQueueing)
        {
            // queueing disabled
            return new()
            {
                Server = server,
                Password = password,
                ResultCode = JoinServerResult.ServerFull
            };
        }

        bool joinedQueue = await _matchmakingService.JoinQueueAsync(server, null, password);
        return new()
        {
            Server = server,
            Password = password,
            ResultCode = joinedQueue ? JoinServerResult.QueueJoined : JoinServerResult.QueueUnavailable
        };
    }

    protected virtual ValueTask<bool> OnGameNotRunning(IServerInfo server)
    {
        return ValueTask.FromResult(!_h2mCommunicationService.GameDetection.IsGameDetectionRunning);
    }

    public async Task<ServerJoinResult> JoinServer(IServerInfo server)
    {
        if (_h2mCommunicationService.GameDetection.DetectedGame is null && 
            !await OnGameNotRunning(server))
        {
            return new ServerJoinResult()
            {
                Server = server,
                ResultCode = JoinServerResult.GameNotRunning
            };
        }

        if (!server.HasMap && !await OnMissingMap(server))
        {
            return new ServerJoinResult()
            {
                Server = server,
                ResultCode = JoinServerResult.MissingMap
            };
        }

        string? password = null;
        if (server.IsPrivate)
        {
            password = await OnPasswordRequired(server);
            if (password is null)
            {
                return new()
                {
                    Server = server,
                    ResultCode = JoinServerResult.NoPassword
                };
            }
        }

        int privilegedSlots = server.PrivilegedSlots < 0 ? 0 : server.PrivilegedSlots;
        int assumedMaxClients = server.MaxClients - privilegedSlots;
        if (server.RealPlayerCount >= assumedMaxClients)
        {
            // server is full (TODO: check again if refresh was long ago to avoid unnecessary server communication?)

            return await OnServerFull(server, password);
        }

        bool joinedSuccessfully = await JoinServer(server, password);

        return new ServerJoinResult()
        {
            Server = server,
            Password = password,
            ResultCode = joinedSuccessfully ? JoinServerResult.Success : JoinServerResult.JoinFailed
        };
    }

    public Task<bool> JoinLastServer()
    {
        if (LastServer is null)
        {
            return Task.FromResult(false);
        }

        return JoinServer(LastServer, _lastServerPassword?.ToUnsecuredString());
    }

    public async Task<bool> JoinServer(IServerConnectionDetails server, string? password)
    {
        bool hasJoined = await _h2mCommunicationService.JoinServer(server.Ip, server.Port.ToString(), password);
        if (hasJoined)
        {
            LastServer = server;
            _lastServerPassword = password?.ToSecuredString();

            OnServerJoined(server);
        }

        return hasJoined;
    }

    void IRecipient<JoinRequestMessage>.Receive(JoinRequestMessage message)
    {
        message.Reply(JoinServer(message.Server, message.Password));
    }
}
