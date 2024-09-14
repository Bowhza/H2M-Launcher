using MatchmakingServer.Authentication;
using MatchmakingServer.Queueing;

using Microsoft.AspNetCore.Authorization;
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
        public IActionResult GetAllQueues([FromQuery] QueueProcessingState? state = null)
        {
            IEnumerable<GameServer> filteredQueues = state is null
                ? _queueingService.QueuedServers
                : _queueingService.QueuedServers.Where(s => s.ProcessingState == state);

            return Ok(filteredQueues.Select(s =>
            {
                return new
                {
                    s.InstanceId,
                    s.ServerIp,
                    s.ServerPort,
                    Players = s.PlayerQueue.Select(p =>
                    {
                        return new
                        {
                            p.ConnectionId,
                            p.Name,
                            p.State,
                            p.TimeInQueue,
                            JoinAttempts = p.JoinAttempts.Count,
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

        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        [HttpPost("cleanup")]
        public IActionResult CleanupQueues()
        {
            int numQueuesCleanedUp = _queueingService.CleanupZombieQueues();

            return Ok(numQueuesCleanedUp);
        }

        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        [HttpPost("{instanceId}/clear")]
        public async Task<IActionResult> ClearQueue(string instanceId)
        {
            GameServer? server = _queueingService.QueuedServers.FirstOrDefault(s => s.InstanceId == instanceId);

            return server is null ? NotFound() : Ok(await _queueingService.ClearQueue(server));
        }

        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        [HttpPost("{instanceId}/halt")]
        public async Task<IActionResult> HaltQueue(string instanceId)
        {
            GameServer? server = _queueingService.QueuedServers.FirstOrDefault(s => s.InstanceId == instanceId);

            if (server is null)
            {
                return NotFound();
            }

            await _queueingService.HaltQueue(server);

            return Ok();
        }

        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        [HttpDelete("{instanceId}")]
        public async Task<IActionResult> DestroyQueue(string instanceId)
        {
            GameServer? server = _queueingService.QueuedServers.FirstOrDefault(s => s.InstanceId == instanceId);

            if (server is null)
            {
                return NotFound();
            }

            await _queueingService.DestroyQueue(server, remove: true);

            return NoContent();
        }
    }
}
