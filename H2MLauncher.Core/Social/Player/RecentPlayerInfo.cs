using System.Diagnostics.CodeAnalysis;

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

    [SetsRequiredMembers]
    public RecentPlayerInfo(ServerPlayerInfo serverPlayer, SimpleServerInfo server) : base(serverPlayer)
    {
        Server = server;
    }

    public RecentPlayerInfo() { }
}