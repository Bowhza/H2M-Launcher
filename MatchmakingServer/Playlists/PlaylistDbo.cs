using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;

namespace MatchmakingServer.Playlists;

public class PlaylistDbo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public List<string> GameModes { get; init; } = [];

    public List<string> MapPacks { get; init; } = [];

    public List<ServerConnectionDetails> Servers { get; init; } = [];

    public Playlist ToPlaylistDto(int playerCount = 0)
    {
        return new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            GameModes = GameModes,
            MapPacks = MapPacks,
            Servers = Servers,
            CurrentPlayerCount = playerCount,
        };
    }
}
