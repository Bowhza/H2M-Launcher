using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Social.Player;

/// <summary>
/// Information about a recent player encountered on a server.
/// </summary>
/// <param name="Id">The user id of the player.</param>
/// <param name="UserName">The user name of the player.</param>
/// <param name="PlayerName">The current in-game name of the player.</param>
/// <param name="EncounterDate">When the player was encountered on a server.</param>
/// <param name="Server">The server this player was encountered on.</param>
public record RecentPlayerInfo(
    string Id,
    string UserName,
    string? PlayerName,
    SimpleServerInfo Server,
    DateTimeOffset EncounterDate) : ServerPlayerInfo(Id, UserName, PlayerName, EncounterDate);