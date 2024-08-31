using H2MLauncher.Core.Models;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class MatchmakingService
    {
        private readonly H2MCommunicationService _h2MCommunicationService;

        private readonly HubConnection _connection;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(ILogger<MatchmakingService> logger, H2MCommunicationService h2MCommunicationService)
        {
            _logger = logger;

            _connection = new HubConnectionBuilder()
                    .WithUrl("http://localhost:5041/Queue")
                    .Build();

            _connection.On("NotifyJoin", (string ip, int port) =>
            {
                // TODO: join the server
                logger.LogInformation("Received 'NotifyJoin' with {ip} and {port}", ip, port);

                if (h2MCommunicationService.JoinServer(ip, port.ToString()))
                {
                    // simulate join time
                    Task.Delay(5000).ContinueWith(async _ =>
                    {
                        try
                        {
                            await _connection.SendAsync("JoinAck", ip, port, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error while sending join ack");
                        }
                    });

                    return true;
                }

                return false;
            });

            _connection.On("QueuePositionChanged", (int position, int totalPlayersInQueue) =>
            {
                logger.LogInformation("Received update queue position {queuePosition}/{queueLength}", position, totalPlayersInQueue);
            });

            _h2MCommunicationService = h2MCommunicationService;
        }

        public async Task StartConnection()
        {
            await _connection.StartAsync();
        }

        public async Task<bool> JoinQueueAsync(IW4MServer server, string playerName)
        {
            try
            {
                if (_connection.State is not HubConnectionState.Connected)
                {
                    await StartConnection();
                }

                bool joinedSuccesfully = await _connection.InvokeAsync<bool>("JoinQueue", server.Ip, server.Port, server.Instance.Id, playerName);
                return joinedSuccesfully;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
