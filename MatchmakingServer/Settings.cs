using H2MLauncher.Core.Models;

namespace MatchmakingServer;

public record Settings
{
    public required string IW4MAdminMasterApiUrl { get; init; }
}

public record ServerSettings
{
    public List<ServerData> ServerDataList { get; init; } = [];

    public List<Playlist> Playlists { get; init; } = [];

    public int PlayerCountCacheExpirationInS { get; init; } = 120;
}