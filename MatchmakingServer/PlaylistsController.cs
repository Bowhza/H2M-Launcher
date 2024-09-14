using H2MLauncher.Core.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MatchmakingServer
{
    [ApiController]
    [Route("[controller]")]
    public class PlaylistsController : ControllerBase
    {
        private readonly IOptionsMonitor<ServerSettings> _serverSettings;
        private readonly IWebHostEnvironment _env;

        public PlaylistsController(IOptionsMonitor<ServerSettings> serverSettings, IWebHostEnvironment env)
        {
            _serverSettings = serverSettings;
            _env = env;
        }

        [HttpGet("{id}")]
        public IActionResult GetPlaylistById(string id)
        {
            Playlist? playlist = _serverSettings.CurrentValue.Playlists.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (playlist is null)
            {
                return NotFound();
            }

            return Ok(playlist);
        }

        [HttpGet]
        public IActionResult GetAllPlaylists()
        {
            return Ok(_serverSettings.CurrentValue.Playlists.Select(playlist =>
                playlist with
                {
                    Servers = null,
                    ServerCount = playlist.Servers?.Count ?? 0
                }));
        }
    }
}
