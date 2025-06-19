using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Social.Player;

/// <summary>
/// Information about a recent player encountered on a server.
/// </summary>
public record RecentPlayerInfo : ServerPlayerInfo
{
    /// <summary>
    /// The server this player was encountered on.
    /// </summary>
    public required SimpleServerInfo Server { get; init; }

    public RecentPlayerInfo(ServerPlayerInfo serverPlayer) : base(serverPlayer) { }
}