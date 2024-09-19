using System.Security.AccessControl;

using Flurl;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Settings;

using MatchmakingServer.Core.Party;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Party
{
    internal class PartyService : IPartyClient, IDisposable
    {
        private readonly HubConnection _connection;
        private readonly IPartyHub _hub;
        private readonly IDisposable _clientRegistration;

        private readonly ILogger<PartyService> _logger;

        private string? _currentPartyId;

        public PartyService(IOptions<MatchmakingSettings> matchmakingSettings, IPlayerNameProvider playerNameProvider, ILogger<PartyService> logger)
        {
            object queryParams = new
            {
                uid = Guid.NewGuid().ToString(),
                playerName = playerNameProvider.PlayerName
            };

            _connection = new HubConnectionBuilder()
                .WithUrl(matchmakingSettings.Value.QueueingHubUrl.SetQueryParams(queryParams))
                .Build();

            _connection.Closed += Connection_Closed;

            _hub = _connection.CreateHubProxy<IPartyHub>();
            _clientRegistration = _connection.Register<IPartyClient>(this);

            _logger = logger;
        }

        public async Task StartConnection(CancellationToken cancellationToken = default)
        {
            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> CreateParty()
        {
            _currentPartyId = await _hub.CreateParty();
            
            return _currentPartyId;
        }

        #region RPC Handlers

        public Task OnConnectionRejected(string reason)
        {
            _logger.LogError("Connection to party hub was rejected. Reason: {reason}", reason);
            return Task.CompletedTask;
        }

        public Task OnJoinServer(ServerConnectionDetails server)
        {
            throw new NotImplementedException();
        }

        public Task OnKickedFromParty()
        {
            throw new NotImplementedException();
        }

        public Task OnPartyClosed()
        {
            throw new NotImplementedException();
        }

        public Task OnUserJoinedParty(string id, string playerName)
        {
            throw new NotImplementedException();
        }

        public Task OnUserLeftParty(string id, string playerName)
        {
            throw new NotImplementedException();
        }

        public Task OnUserNameChanged(string id, string newPlayerName)
        {
            throw new NotImplementedException();
        }

        #endregion

        private Task Connection_Closed(Exception? exception)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _clientRegistration.Dispose();
        }
    }
}
