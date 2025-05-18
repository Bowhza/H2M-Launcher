using H2MLauncher.Core.Models;

using MatchmakingServer.Playlists;

namespace MatchmakingServer;

public record Settings
{
    public required string IW4MAdminMasterApiUrl { get; init; }

    public string HMWMasterServerUrl { get; init; } = string.Empty;
}

public record ServerSettings
{
    public List<ServerData> ServerDataList { get; init; } = [];

    public List<PlaylistDbo> Playlists { get; init; } = [];

    public int PlayerCountCacheExpirationInS { get; init; } = 120;
}