﻿using System.Diagnostics.CodeAnalysis;

using Flurl;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;

using MatchmakingServer.Core.Party;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Party
{
    public class PartyService : IPartyClient, IHubConnectionObserver, IDisposable
    {
        private readonly HubConnection _connection;
        private readonly IPartyHub _hubProxy;
        private readonly IDisposable _clientRegistration;

        private readonly IServerJoinService _serverJoinService;
        private readonly ILogger<PartyService> _logger;

        public bool IsConnected => _connection.State is HubConnectionState.Connected;

        public event Action? KickedFromParty;
        public event Action? PartyClosed;
        public event Action<bool>? ConnectionChanged;
        public event Action? PartyChanged;

        private readonly string _currentClientId;
        private PartyInfo? _currentParty;
        private bool _isPartyLeader;

        public string? PartyId => _currentParty?.PartyId;
        public IReadOnlyList<PartyPlayerInfo>? Members => _currentParty?.Members.AsReadOnly();

        [MemberNotNullWhen(true, nameof(PartyId))]
        [MemberNotNullWhen(true, nameof(Members))]
        public bool IsPartyActive => _currentParty is not null;
        public bool IsPartyLeader => _isPartyLeader;

        public string CurrentClientId => _currentClientId;

        public PartyService(
            IOptions<MatchmakingSettings> matchmakingSettings,
            IPlayerNameProvider playerNameProvider,
            IServerJoinService serverJoinService,
            ILogger<PartyService> logger)
        {
            _currentClientId = Guid.NewGuid().ToString();

            object queryParams = new
            {
                uid = _currentClientId,
                playerName = playerNameProvider.PlayerName
            };

            _connection = new HubConnectionBuilder()
                .WithUrl(matchmakingSettings.Value.PartyHubUrl.SetQueryParams(queryParams))
                .Build();

            _hubProxy = _connection.CreateHubProxy<IPartyHub>();
            _clientRegistration = _connection.Register<IPartyClient>(this);

            _serverJoinService = serverJoinService;
            _logger = logger;

            _serverJoinService.ServerJoined += ServerJoinService_ServerJoined;
        }

        /// <summary>
        /// Called when we joined a server.
        /// </summary>
        private async void ServerJoinService_ServerJoined(ISimpleServerInfo serverInfo)
        {
            if (_currentParty is null || !_isPartyLeader)
            {
                // if we are not a party leader do nothing
                return;
            }

            try
            {
                // notify the server that the party server changed
                await _hubProxy.JoinServer(new()
                {
                    ServerIp = serverInfo.Ip,
                    ServerPort = serverInfo.Port,
                    ServerName = serverInfo.ServerName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while notifying party service of joined server {server}", serverInfo);
            }
        }

        public async Task StartConnection(CancellationToken cancellationToken = default)
        {
            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> CreateParty()
        {
            if (_connection.State is HubConnectionState.Disconnected)
            {
                await StartConnection();
            }

            _logger.LogDebug("Creating party...");

            _currentParty = await _hubProxy.CreateParty();

            if (_currentParty is not null)
            {
                _isPartyLeader = true;
                PartyChanged?.Invoke();
            }

            return _currentParty?.PartyId;
        }

        public async Task JoinParty(string partyId)
        {
            if (_connection.State is HubConnectionState.Disconnected)
            {
                await StartConnection();
            }

            using var _ = _logger.BeginPropertyScope(partyId);
            _logger.LogDebug("Joining party...");

            PartyInfo? party = await _hubProxy.JoinParty(partyId);
            if (party is null)
            {
                _logger.LogDebug("Could not join party");
                return;
            }

            _currentParty = party;
            _isPartyLeader = false;
            PartyChanged?.Invoke();

            _logger.LogInformation("Joined party");

            if (party.Server is null)
            {
                return;
            }

            _logger.LogDebug("Party has a server, joining {server}...", party.Server);

            await _serverJoinService.JoinServer(party.Server, null);
        }

        public async Task LeaveParty()
        {
            if (_currentParty is null)
            {
                return;
            }

            if (!await _hubProxy.LeaveParty())
            {
                _logger.LogWarning("Could not leave party {partyId}", _currentParty?.PartyId);
            }

            _currentParty = null;
            _isPartyLeader = false;
            PartyChanged?.Invoke();

            _logger.LogDebug("Party left");
        }

        #region RPC Handlers

        public Task OnConnectionRejected(string reason)
        {
            _logger.LogError("Connection to party hub was rejected. Reason: {reason}", reason);
            return Task.CompletedTask;
        }

        public Task OnServerChanged(SimpleServerInfo server)
        {
            _logger.LogDebug("Party server changed: {server}, joining...", server);

            return _serverJoinService.JoinServer(server, null);
        }

        public Task OnKickedFromParty()
        {
            _logger.LogDebug("Kicked from the party {partyId}", _currentParty?.PartyId);

            _currentParty = null;
            KickedFromParty?.Invoke();

            return Task.CompletedTask;
        }

        public Task OnPartyClosed()
        {
            _logger.LogDebug("Party {partyId} closed", _currentParty?.PartyId);

            _currentParty = null;
            _isPartyLeader = false;
            PartyClosed?.Invoke();
            PartyChanged?.Invoke();

            return Task.CompletedTask;
        }

        public Task OnUserJoinedParty(string id, string playerName)
        {
            _logger.LogDebug("Player {playerName} ({playerId}) joined the party", playerName, id);

            if (_currentParty is not null)
            {
                _currentParty.Members.Add(new PartyPlayerInfo(id, playerName, IsLeader: false));
                PartyChanged?.Invoke();
            }

            return Task.CompletedTask;
        }

        public Task OnUserLeftParty(string id)
        {
            _logger.LogDebug("Player ({playerId}) left the party", id);

            if (_currentParty is null)
            {
                return Task.CompletedTask;
            }

            PartyPlayerInfo? member = _currentParty.Members.Find(m => m.Id == id);
            if (member is null)
            {
                return Task.CompletedTask;
            }

            if (_currentParty.Members.Remove(member))
            {
                PartyChanged?.Invoke();
            }

            return Task.CompletedTask;
        }

        public Task OnUserNameChanged(string id, string newPlayerName)
        {
            return Task.CompletedTask;
        }

        #endregion

        public void Dispose()
        {
            _clientRegistration.Dispose();
        }

        public Task OnClosed(Exception? exception)
        {
            _logger.LogDebug(exception, "Party hub connection closed");

            _currentParty = null;
            _isPartyLeader = false;

            ConnectionChanged?.Invoke(false);

            return Task.CompletedTask;
        }

        public Task OnReconnected(string? connectionId)
        {
            return Task.CompletedTask;
        }

        public Task OnReconnecting(Exception? exception)
        {
            return Task.CompletedTask;
        }
    }
}
