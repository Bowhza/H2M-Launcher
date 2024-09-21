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
    internal class PartyService : IPartyClient, IHubConnectionObserver, IDisposable
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
        //public event Action<PartyInfo>? PartyChanged;

        private PartyInfo? _currentParty;

        public PartyService(
            IOptions<MatchmakingSettings> matchmakingSettings,
            IPlayerNameProvider playerNameProvider,
            IServerJoinService serverJoinService,
            ILogger<PartyService> logger)
        {
            object queryParams = new
            {
                uid = Guid.NewGuid().ToString(),
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

        private void ServerJoinService_ServerJoined(IServerConnectionDetails server)
        {
            if (_currentParty is null)
            {
                return;
            }

            if (server is ISimpleServerInfo serverInfo)
            {
                _hubProxy.JoinServer(new() { ServerIp = serverInfo.Ip, ServerName = serverInfo.ServerName, ServerPort = serverInfo.Port });
            }
            else
            {
                _hubProxy.JoinServer(new() { ServerIp = server.Ip, ServerPort = server.Port, ServerName = "", });
            }
        }

        public async Task StartConnection(CancellationToken cancellationToken = default)
        {
            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> CreateParty()
        {
            _logger.LogDebug("Creating party...");
            _currentParty = await _hubProxy.CreateParty();

            return _currentParty?.PartyId;
        }

        public async Task JoinParty(string partyId)
        {
            using var _ = _logger.BeginPropertyScope(partyId);
            _logger.LogDebug("Joining party...");

            PartyInfo? party = await _hubProxy.JoinParty(partyId);
            if (party is null)
            {
                _logger.LogDebug("Could not join party");
                return;
            }

            _logger.LogInformation("Joined party");

            if (party.Server is null)
            {
                return;
            }

            _logger.LogDebug("Party has a server, joining {server}...", party.Server);

            await _serverJoinService.JoinServer(party.Server, null);
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
            PartyClosed?.Invoke();

            return Task.CompletedTask;
        }

        public Task OnUserJoinedParty(string id, string playerName)
        {
            _logger.LogDebug("Player {playerName} ({playerId}) joined the party", playerName, id);

            _currentParty?.Members.Add(new PartyPlayerInfo(id, playerName, IsLeader: false));

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

            _currentParty.Members.Remove(member);

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
