namespace H2MLauncher.Core.Social.Player;

/// <summary>
/// Information about a player encountered on a server.
/// </summary>
public record ServerPlayerInfo : PlayerInfo
{
    /// <summary>
    /// When the player was encountered on a server.
    /// </summary>
    public required DateTimeOffset EncounterDate { get; init; }
}
