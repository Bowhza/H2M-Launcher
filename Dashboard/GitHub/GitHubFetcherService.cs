using Dashboard.Database;
using Dashboard.Database.Entities;
using Dashboard.Downloads;

namespace Dashboard.GitHub;

public class GitHubFetcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;

    public GitHubFetcherService(IServiceScopeFactory scopeFactory, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await FetchAndStoreDownloadCounts();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task FetchAndStoreDownloadCounts()
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        DatabaseContext context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        IGitHubApiClient gitHubApiClient = scope.ServiceProvider.GetRequiredService<IGitHubApiClient>();

        var releases = await gitHubApiClient.GetRepositoryReleasesAsync("Bowhza", "H2M-Launcher");
        foreach (var release in releases)
        {
            if (release.Assets?.Count > 0)
            {
                var downloadCount = new DownloadCount
                {
                    Tag = release.TagName,
                    Timestamp = DateTime.UtcNow,
                    Count = release.Assets[0].DownloadCount,
                    ReleaseDate = release.CreatedAt,
                };

                await _eventBus.PublishAsync(new DownloadCountAddedNotification(downloadCount));

                context.DownloadCounts.Add(downloadCount);
            }
        }
        await context.SaveChangesAsync();
    }
}