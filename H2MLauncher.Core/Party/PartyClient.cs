using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking;
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
    public sealed class PartyClient : HubClient<IPartyHub>, IPartyClient, IDisposable
    {
        private readonly IDisposable _clientRegistration;
        private readonly SemaphoreSlim _joinCreateLock = new(1, 1);

        private readonly IServerJoinService _serverJoinService;
        private readonly IPlayerNameProvider _playerNameProvider;
        private readonly MatchmakingService _matchmakingService;
        private readonly ILogger<PartyClient> _logger;

        private readonly bool _autoCreateParty = true;
        private PartyInfo? _currentParty;
        private bool _isPartyLeader;
        private readonly string _clientId;

        public string? PartyId => _currentParty?.PartyId;
        public IReadOnlyList<PartyPlayerInfo>? Members => _currentParty?.Members.AsReadOnly();

        [MemberNotNullWhen(true, nameof(PartyId))]
        [MemberNotNullWhen(true, nameof(Members))]
        [MemberNotNullWhen(true, nameof(_currentParty))]
        public bool IsPartyActive => _currentParty is not null;
        public bool IsPartyLeader => _isPartyLeader;


        public event Action? KickedFromParty;
        public event Action? PartyClosed;
        public event Action? PartyChanged;
        public event Action<PartyPlayerInfo>? UserJoined;
        public event Action<PartyPlayerInfo>? UserLeft;
        public event Action<PartyPlayerInfo>? UserChanged;

        public PartyClient(
            IPlayerNameProvider playerNameProvider,
            IServerJoinService serverJoinService,
            MatchmakingService matchmakingService,
            ILogger<PartyClient> logger,
            HubConnection hubConnection,
            IOnlineServices onlineService) : base(hubConnection)
        {
            _clientId = onlineService.ClientContext.ClientId;
            _clientRegistration = hubConnection.Register<IPartyClient>(this);

            _serverJoinService = serverJoinService;
            _matchmakingService = matchmakingService;
            _playerNameProvider = playerNameProvider;
            _logger = logger;

            _serverJoinService.ServerJoined += ServerJoinService_ServerJoined;
        }

        protected override IPartyHub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken)
        {
            return hubConnection.CreateHubProxy<IPartyHub>(hubCancellationToken);
        }

        private async void PlayerNameProvider_PlayerNameChanged(string oldName, string newName)
        {
            try
            {
                if (Connection.State is HubConnectionState.Disconnected)
                {
                    return;
                }

                if (IsPartyActive)
                {
                    int selfIndex = _currentParty.Members.FindIndex(m => m.Id == _clientId);
                    if (selfIndex != -1)
                    {
                        PartyPlayerInfo self = _currentParty.Members[selfIndex];
                        PartyPlayerInfo newSelf = self with
                        {
                            Name = newName
                        };

                        _currentParty.Members[selfIndex] = newSelf;
                        UserChanged?.Invoke(newSelf);
                    }
                }

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
            if (_currentParty is null || !_isPartyLeader || Connection.State is HubConnectionState.Disconnected)
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
            await _joinCreateLock.WaitAsync();
            try
            {                
                if (!await StartConnection())
                {
                    return null;
                }

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
            finally
            {
                _joinCreateLock.Release();
            }
        }

        public async Task<bool> JoinParty(string partyId)
        {
            await _joinCreateLock.WaitAsync();
            try
            {
                if (!await StartConnection())
                {
                    return false;
                }

                using var _ = _logger.BeginPropertyScope(partyId);
                _logger.LogDebug("Joining party...");

                PartyInfo? party = await Hub.JoinParty(partyId);
                if (party is null)
                {
                    _logger.LogDebug("Could not join party");
                    return false;
                }

                _currentParty = party;
                _isPartyLeader = false;
                PartyChanged?.Invoke();

                _logger.LogInformation("Joined party.");

                // make sure matchmaking service is connected to allow party matchmaking
                await _matchmakingService.StartConnection();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while joining party");
                return false;
            }
            finally
            {
                _joinCreateLock.Release();
            }
        }

        public async Task LeaveParty()
        {
            await _joinCreateLock.WaitAsync();
            try
            {
                if (_currentParty is null)
                {
                    return;
                }

                if (!await StartConnection())
                {
                    return;
                }

                if (!await Hub.LeaveParty())
                {
                    _logger.LogWarning("Could not leave party {partyId}", _currentParty?.PartyId);
                }

                bool wasPartyLeader = _isPartyLeader;

                _currentParty = null;
                _isPartyLeader = false;
                PartyChanged?.Invoke();

                _logger.LogDebug("Party left");

                if (_autoCreateParty && !wasPartyLeader)
                {
                    _ = CreateParty();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while leaving party");
            }
            finally
            {
                _joinCreateLock.Release();
            }
        }

        public async Task KickMember(string id)
        {
            if (_currentParty is null)
            {
                return;
            }

            if (!await StartConnection())
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

        protected override Task OnConnected(CancellationToken cancellationToken = default)
        {
            _playerNameProvider.PlayerNameChanged += PlayerNameProvider_PlayerNameChanged;
            return base.OnConnected(cancellationToken);
        }

        protected override Task OnConnectionClosed(Exception? exception)
        {
            _currentParty = null;
            _isPartyLeader = false;
            _playerNameProvider.PlayerNameChanged -= PlayerNameProvider_PlayerNameChanged;
            return base.OnConnectionClosed(exception);
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

            if (_autoCreateParty)
            {
                _ = CreateParty();
            }

            return Task.CompletedTask;
        }

        Task IPartyClient.OnPartyClosed()
        {
            _logger.LogDebug("Party {partyId} closed", _currentParty?.PartyId);

            _currentParty = null;
            _isPartyLeader = false;
            PartyClosed?.Invoke();
            PartyChanged?.Invoke();

            if (_autoCreateParty)
            {
                _ = CreateParty();
            }

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
