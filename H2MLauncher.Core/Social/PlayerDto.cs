namespace MatchmakingServer.Core.Social;

public record PlayerDto(
    string Id,
    string UserName,
    string? PlayerName,
    GameStatus GameStatus,
    string? PartyId);
