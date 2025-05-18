using Microsoft.Extensions.Options;

namespace MatchmakingServer.Playlists;

/// <summary>
/// Populates the playlist store with predefined playlists, if empty.
/// </summary>
public sealed class PlaylistsSeedingService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<ServerSettings> _options;
    private readonly ILogger<PlaylistsSeedingService> _logger;

    public PlaylistsSeedingService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ServerSettings> options,
        ILogger<PlaylistsSeedingService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        try
        {
            PlaylistStore playlistStore = scope.ServiceProvider.GetRequiredService<PlaylistStore>();
            if (playlistStore.PlaylistsCount > 0)
            {
                return;
            }

            List<PlaylistDbo> playlists = _options.Value.Playlists;

            _logger.LogDebug("Playlist store is empty, seeding {numPlaylists} playlists...", playlists.Count);

            if (await playlistStore.SeedPlaylists(playlists))
            {
                _logger.LogInformation("Successfully seeded {numPlaylists} playlists", playlists.Count);
            }
            else
            {
                _logger.LogWarning("Could not seed playlists");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while seeding playlists");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
