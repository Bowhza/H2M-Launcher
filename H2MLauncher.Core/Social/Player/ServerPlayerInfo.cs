namespace H2MLauncher.Core.Social.Player;

/// <summary>
/// Information about a player encountered on a server.
/// </summary>
/// <param name="Id">The user id of the player.</param>
/// <param name="UserName">The user name of the player.</param>
/// <param name="PlayerName">The current in-game name of the player.</param>
/// <param name="EncounterDate">When the player was encountered on a server.</param>
public record ServerPlayerInfo(string Id, string UserName, string? PlayerName, DateTimeOffset EncounterDate)
    : PlayerInfo(Id, UserName, PlayerName);
