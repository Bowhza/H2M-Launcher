using H2MLauncher.Core.Models;
using H2MLauncher.Core.Social.Player;

using MatchmakingServer.Authentication;
using MatchmakingServer.Core.Social;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingServer.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly PlayerStore _playerStore;

    public PlayersController(PlayerStore playerStore)
    {
        _playerStore = playerStore;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlayerDto>>> GetPlayers()
    {
        IList<Player> players = await _playerStore.GetAllPlayers();

        return Ok(players.Select(CreatePlayerDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlayerDto>> GetPlayerByUserId(string id)
    {
        Player? player = await _playerStore.TryGet(id);
        if (player is null)
        {
            return NotFound();
        }
        return Ok(CreatePlayerDto(player));
    }

    private static PlayerDto CreatePlayerDto(Player player)
    {
        return new PlayerDto(
            player.Id,
            player.UserName,
            player.Name,
            player.GameStatus,
            player.Party?.Id,
            player.PlayingServer is not null
                ? CreatePlayingServerDto(player.PlayingServer, player.PlayingServerJoinDate)
                : null
        );
    }

    private static PlayingServerDto CreatePlayingServerDto(GameServer gameServer, DateTimeOffset? joinedAt)
    {
        return new(
            gameServer.ServerIp,
            gameServer.ServerPort,
            gameServer.LastServerInfo?.HostName ?? gameServer.ServerName,
            joinedAt ?? default);
    }
}