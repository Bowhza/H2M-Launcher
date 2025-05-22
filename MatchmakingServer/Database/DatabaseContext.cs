using Microsoft.EntityFrameworkCore;

namespace MatchmakingServer.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
}
