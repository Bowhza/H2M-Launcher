using H2MLauncher.Core.Social.Player;

using MatchmakingServer.Authentication;
using MatchmakingServer.Core.Social;
using MatchmakingServer.Database;
using MatchmakingServer.Database.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingServer.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly DatabaseContext _dbContext;

    public UsersController(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlayerDto>>> GetAllUsers()
    {
        IList<UserDto> users = await _dbContext.Users
            .Select(u => CreateUserDto(u))
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlayerDto>> GetUserById(Guid id)
    {
        UserDto? user = await _dbContext.Users
            .Select(u => CreateUserDto(u))
            .FirstOrDefaultAsync(u => u.Id == id); 

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    private static UserDto CreateUserDto(UserDbo user)
    {
        return new UserDto(
            user.Id,
            user.Name,
            user.LastPlayerName,
            user.CreationDate
        );
    }
}
