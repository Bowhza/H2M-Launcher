using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.Utilities;
using H2MLauncher.Core.Utilities.SignalR;

using MatchmakingServer.Core.Party;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Party
{
    public record PartyState
    {
        public PartyInfo? Party { get; }
        public bool IsPartyLeader { get; }
        public bool IsPartyActive => Party is not null;
    }

    public sealed class PartyClient : HubClient<IPartyHub>, IPartyClient, IDisposable
    {
        private readonly IDisposable _clientRegistration;

        private readonly IServerJoinService _serverJoinService;
        private readonly IPlayerNameProvider _playerNameProvider;
        private readonly ILogger<PartyClient> _logger;

        public event Action? KickedFromParty;
        public event Action? PartyClosed;
        public event Action<PartyPlayerInfo>? UserJoined;
        public event Action<PartyPlayerInfo>? UserLeft;
        public event Action<PartyPlayerInfo>? UserChanged;

        public event Action? PartyChanged;

        private PartyInfo? _currentParty;
        private bool _isPartyLeader;
        private readonly string _clientId;

        public string? PartyId => _currentParty?.PartyId;
        public IReadOnlyList<PartyPlayerInfo>? Members => _currentParty?.Members.AsReadOnly();

        [MemberNotNullWhen(true, nameof(PartyId))]
        [MemberNotNullWhen(true, nameof(Members))]
        public bool IsPartyActive => _currentParty is not null;
        public bool IsPartyLeader => _isPartyLeader;

        public PartyClient(
            IPlayerNameProvider playerNameProvider,
            IServerJoinService serverJoinService,
            ILogger<PartyClient> logger,
            HubConnection hubConnection,
            IOnlineServices onlineService) : base(hubConnection)
        {
            _clientId = onlineService.ClientContext.ClientId;

            _clientRegistration = hubConnection.Register<IPartyClient>(this);

            _serverJoinService = serverJoinService;
            _playerNameProvider = playerNameProvider;
            _logger = logger;

            _serverJoinService.ServerJoined += ServerJoinService_ServerJoined;
            _playerNameProvider.PlayerNameChanged += PlayerNameProvider_PlayerNameChanged;
        }

        protected override IPartyHub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken)
        {
            return hubConnection.CreateHubProxy<IPartyHub>(hubCancellationToken);
        }

        private async void PlayerNameProvider_PlayerNameChanged(string oldName, string newName)
        {
            try
            {

                await Hub.UpdatePlayerName(newName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating player name from {oldName} to {newName}.", oldName, newName);
            }
        }

        /// <summary>
        /// Called when we joined a server.
        /// </summary>
        private async void ServerJoinService_ServerJoined(ISimpleServerInfo serverInfo, JoinKind kind)
        {
            if (_currentParty is null || !_isPartyLeader)
            {
                // if we are not a party leader do nothing
                return;
            }

            if (kind is not (JoinKind.Normal or JoinKind.Forced))
            {
                // only propagate normal or fore joins as
                // queue join is handled by the server
                return;
            }

            try
            {
                // notify the server that the party server changed
                await Hub.JoinServer(new()
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

        public async Task<string?> CreateParty()
        {
            try
            {
                await StartConnection();

                _logger.LogDebug("Creating party...");

                _currentParty = await Hub.CreateParty();

                if (_currentParty is not null)
                {
                    _isPartyLeader = true;
                    PartyChanged?.Invoke();
                }

                return _currentParty?.PartyId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating party");
                return null;
            }
        }

        public async Task JoinParty(string partyId)
        {
            try
            {
                await StartConnection();

                using var _ = _logger.BeginPropertyScope(partyId);
                _logger.LogDebug("Joining party...");

                PartyInfo? party = await Hub.JoinParty(partyId);
                if (party is null)
                {
                    _logger.LogDebug("Could not join party");
                    return;
                }

                _currentParty = party;
                _isPartyLeader = false;
                PartyChanged?.Invoke();

                _logger.LogInformation("Joined party");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while joining party");
            }
        }

        public async Task LeaveParty()
        {
            if (_currentParty is null)
            {
                return;
            }

            if (!await Hub.LeaveParty())
            {
                _logger.LogWarning("Could not leave party {partyId}", _currentParty?.PartyId);
            }

            _currentParty = null;
            _isPartyLeader = false;
            PartyChanged?.Invoke();

            _logger.LogDebug("Party left");
        }

        public async Task KickMember(string id)
        {
            if (_currentParty is null)
            {
                return;
            }

            if (!await Hub.KickPlayer(id))
            {
                _logger.LogDebug("Could not kick player {userId} from party", id);
                return;
            }

            // no update needed, OnUserLeftParty() will handle this

            _logger.LogInformation("Player {userId} was kicked from the party", id);
        }

        public bool IsSelf(PartyPlayerInfo member)
        {
            return member.Id == _clientId;
        }

        #region RPC Handlers

        Task IPartyClient.OnConnectionRejected(string reason)
        {
            _logger.LogError("Connection to party hub was rejected. Reason: {reason}", reason);
            return Task.CompletedTask;
        }

        Task IPartyClient.OnServerChanged(SimpleServerInfo server)
        {
            _logger.LogDebug("Party server changed: {server}, joining...", server);

            return _serverJoinService.JoinServer(server, null, JoinKind.FromParty);
        }

        Task IPartyClient.OnKickedFromParty()
        {
            _logger.LogDebug("Kicked from the party {partyId}", _currentParty?.PartyId);

            _currentParty = null;
            KickedFromParty?.Invoke();
            PartyChanged?.Invoke();

            return Task.CompletedTask;
        }

        Task IPartyClient.OnPartyClosed()
        {
            _logger.LogDebug("Party {partyId} closed", _currentParty?.PartyId);

            _currentParty = null;
            _isPartyLeader = false;
            PartyClosed?.Invoke();
            PartyChanged?.Invoke();

            return Task.CompletedTask;
        }

        Task IPartyClient.OnUserJoinedParty(string id, string playerName)
        {
            _logger.LogDebug("Player {playerName} ({playerId}) joined the party", playerName, id);

            if (_currentParty is not null)
            {
                PartyPlayerInfo newUser = new(id, playerName, IsLeader: false);

                _currentParty.Members.Add(newUser);

                UserJoined?.Invoke(newUser);
            }

            return Task.CompletedTask;
        }

        Task IPartyClient.OnUserLeftParty(string id)
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
                UserLeft?.Invoke(member);
            }

            return Task.CompletedTask;
        }

        Task IPartyClient.OnUserNameChanged(string id, string newPlayerName)
        {
            if (_currentParty is null)
            {
                _logger.LogWarning("Received OnUserNameChanged but current party is null");
                return Task.CompletedTask;
            }

            _logger.LogTrace("Received OnUserNameChanged({userId}, {newPlayerName})", id, newPlayerName);

            int memberIndex = _currentParty.Members.FindIndex(m => m.Id == id);
            if (memberIndex == -1)
            {
                _logger.LogWarning("Cannot find party member with id {userId}", id);
                return Task.CompletedTask;
            }

            PartyPlayerInfo member = _currentParty.Members[memberIndex];

            _currentParty.Members[memberIndex] = member with
            {
                Name = newPlayerName
            };

            UserChanged?.Invoke(_currentParty.Members[memberIndex]);

            _logger.LogDebug("Party member {userId} changed name from {oldName} to {newName}", id, member.Name, newPlayerName);

            return Task.CompletedTask;
        }

        #endregion

        public Task OnClosed(Exception? exception)
        {
            _logger.LogDebug(exception, "Party hub connection closed");

            _currentParty = null;
            _isPartyLeader = false;

            PartyChanged?.Invoke();

            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clientRegistration.Dispose();
                _playerNameProvider.PlayerNameChanged -= PlayerNameProvider_PlayerNameChanged;
                _serverJoinService.ServerJoined -= ServerJoinService_ServerJoined;
            }

            base.Dispose(disposing);
        }
    }
}
