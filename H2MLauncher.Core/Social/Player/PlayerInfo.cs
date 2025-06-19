namespace H2MLauncher.Core.Social.Player;

/// <summary>
/// Simple information about a player.
/// </summary>
public record PlayerInfo
{
    public required string Id { get; init; }

    public required string UserName { get; init; }

    public string? PlayerName { get; init; }
}
