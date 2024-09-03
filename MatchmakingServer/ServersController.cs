using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MatchmakingServer
{
    [ApiController]
    [Route("[controller]")]
    public class ServersController : ControllerBase
    {
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;

        public ServersController(IOptionsMonitor<ServerSettings> serverSettings)
        {
            _serverSettings = serverSettings;
        }

        [HttpGet("data")]
        public IActionResult GetAllServerData()
        {
            return Ok(_serverSettings.CurrentValue.ServerDataList);
        }
    }
}
