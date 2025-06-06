using System.Text.Json;

using H2MLauncher.Core.Models;

using MatchmakingServer.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MatchmakingServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("[controller]")] // NOTE: for compatibility
    public class ServersController : ControllerBase
    {
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;
        private readonly IWebHostEnvironment _env;

        public ServersController(IOptionsMonitor<ServerSettings> serverSettings, IWebHostEnvironment env)
        {
            _serverSettings = serverSettings;
            _env = env;
        }

        [HttpGet("data")]
        public IActionResult GetAllServerData()
        {
            return Ok(_serverSettings.CurrentValue.ServerDataList);
        }

        [HttpPut("data")]
        [Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateServerData(ServerData data)
        {
            _serverSettings.CurrentValue.ServerDataList.RemoveAll(s => s.Ip == data.Ip && s.Port == data.Port);
            _serverSettings.CurrentValue.ServerDataList.Add(data);

            if (_env.IsProduction())
            {
                // just export to random file for now in case we restart the server
                string content = JsonSerializer.Serialize(_serverSettings.CurrentValue.ServerDataList);
                await System.IO.File.WriteAllTextAsync("exportedServerData.json", content);
            }

            return Ok();
        }
    }
}
