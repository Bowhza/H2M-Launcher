using H2MLauncher.Core.Models;

namespace MatchmakingServer.Playlists.Dtos;

public record CreatePlaylistDto
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public List<ServerConnectionDetails>? Servers { get; init; } = [];
}
