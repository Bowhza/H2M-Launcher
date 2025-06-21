using H2MLauncher.Core.Social.Status;

namespace H2MLauncher.Core.Social.Player;

public record PlayerDto(
    string Id,
    string UserName,
    string? PlayerName,
    GameStatus GameStatus,    
    string? PartyId,
    PlayingServerDto? PlayingServer);

public record PlayingServerDto(string Ip, int Port, string Name, DateTimeOffset JoinedAt);
