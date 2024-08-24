using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR
{
    public class QueueingHub : Hub<IClient>, IQueueingHub
    {
        private readonly ILogger<QueueingHub> _logger;
        private readonly QueueingService _queueingService;

        public QueueingHub(ILogger<QueueingHub> logger, QueueingService queueingService)
        {
            _logger = logger;
            _queueingService = queueingService;
        }

        public Task<bool> JoinQueue(string serverIp, int serverPort, string playerName)
        {
            if (_queueingService.AddPlayer(Context.ConnectionId, playerName) is null)
            {
                // player already connected
                return Task.FromResult(false);
            }

            return Task.FromResult(_queueingService.JoinQueue(serverIp, serverPort, Context.ConnectionId));
        }

        public Task LeaveQueue()
        {
            _queueingService.LeaveQueue(Context.ConnectionId);

            return Task.CompletedTask;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(exception, "Client disconnected: {connectionId}", Context.ConnectionId);

            var player = _queueingService.RemovePlayer(Context.ConnectionId);
            if (player is null)
            {
                return;
            }

            _logger.LogInformation("Removed player {player}", player);

            await Task.CompletedTask;
        }
    }
}
