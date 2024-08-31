using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Mvc;

namespace MatchmakingServer
{
    [ApiController]
    [Route("[controller]")]
    public class QueuesController : ControllerBase
    {
        private readonly QueueingService _queueingService;

        public QueuesController(QueueingService queueingService)
        {
            _queueingService = queueingService;
        }


        [HttpGet]
        public IActionResult GetAllQueues()
        {
            return Ok(_queueingService.QueuedServers.Select(s =>
            {
                return new
                {
                    s.InstanceId,
                    s.ServerIp,
                    s.ServerPort,
                    Players = s.JoiningPlayers.Select(p =>
                    {
                        return new
                        {
                            p.ConnectionId,
                            p.Name,
                            p.State,
                            JoinAttempts = p.JoinAttempts.Count,
                            QueueTime = DateTimeOffset.Now - p.QueuedAt,
                        };
                    }),
                    s.LastServerInfo?.HostName,
                    s.LastServerInfo?.Ping,
                    s.LastServerInfo?.RealPlayerCount,
                    s.LastServerInfo?.MaxClients,
                    s.SpawnDate,
                    ProcessingState = s.ProcessingState.ToString(),
                };
            }));
        }
    }
}
