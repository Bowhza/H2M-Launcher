namespace H2MLauncher.Core.Social.Player;

/// <summary>
/// Simple information about a player.
/// </summary>
/// <param name="Id">The user id of the player.</param>
/// <param name="UserName">The user name of the player.</param>
/// <param name="PlayerName">The current in-game name of the player.</param>
public record PlayerInfo(string Id, string UserName, string? PlayerName);
