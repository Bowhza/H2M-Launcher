using MatchmakingServer.Database;
using MatchmakingServer.Database.Entities;

using Microsoft.EntityFrameworkCore;

using UniqueNamer;

namespace MatchmakingServer.Social;

public class UserManager
{
    private readonly DatabaseContext _dbContext;

    public UserManager(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserDbo?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public Task<UserDbo?> FindByKeyAsync(string publicKey, CancellationToken cancellationToken)
    {
        return _dbContext.UserKeys
            .Include(uk => uk.User)
            .Where(uk => uk.IsActive && uk.PublicKeySPKI == publicKey)
            .Select(uk => uk.User)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDbo> RegisterNewUserAsync(string publicKey, string publicKeyHash, string? userName, CancellationToken cancellationToken)
    {
        UserKeyDbo userKey = new()
        {
            PublicKeySPKI = publicKey,
            PublicKeyHash = publicKeyHash,
            IsActive = true,
            LastUsedDate = DateTime.UtcNow,
        };

        UserDbo user = new()
        {
            Name = userName ?? GenerateUserName(),
            Keys = [userKey]
        };

        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    private static string GenerateUserName()
    {
        return UniqueNamer.UniqueNamer.Generate(
            Enum.GetValues<Categories>(),
            suffixLength: 2,
            separator: "",
            style: Style.TitleCase);
    }

    public Task UpdateKeyUsageTimestamp(string publicKey, CancellationToken cancellationToken)
    {
        return _dbContext.UserKeys
            .Where(uk => uk.IsActive && uk.PublicKeySPKI == publicKey)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(uk => uk.LastUsedDate, DateTime.UtcNow), cancellationToken
            );
    }

    public Task UpdatePlayerNameAsync(Guid userId, string playerName, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(u => u.LastPlayerName, playerName), cancellationToken
            );
    }
}
