namespace MatchmakingServer.Core.Social;

public record UserDto(
    Guid Id,
    string UserName,
    string? LastPlayerName,
    DateTimeOffset CreatedAt);
