using H2MLauncher.Core.Services;

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

        public Task JoinAck(bool successful)
        {
            if (successful)
            {
                _queueingService.OnPlayerJoinConfirmed(Context.ConnectionId);
            }
            else
            {
                _queueingService.OnPlayerJoinFailed(Context.ConnectionId);
            }

            return Task.CompletedTask;
        }

        public Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId, string playerName)
        {
            _logger.LogTrace("JoinQueue({serverIp}:{serverPort}, {playerName}) triggered", serverIp, serverPort, playerName);

            var player = _queueingService.AddPlayer(Context.ConnectionId, playerName);
            if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                // player already in queue
                _logger.LogWarning("Cannot join queue for {serverIp}:{serverPort}, player {player} already queued", 
                    serverIp, serverPort, player);
                return Task.FromResult(false);
            }

            return _queueingService.JoinQueue(serverIp, serverPort, Context.ConnectionId, instanceId);
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
