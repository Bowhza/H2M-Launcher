using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Matchmaking.Models;

public record CustomPlaylist : Playlist
{
    public required new List<ServerConnectionDetails> Servers
    {
        get => base.Servers ?? [];
        init => base.Servers = value;
    }

    public override bool IsCustom { get; } = true;
}
