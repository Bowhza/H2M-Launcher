using Dashboard.Database;
using Dashboard.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Dashboard;

public class LauncherRelease
{
    public required string Tag { get; init; }

    public required DateTime ReleaseDate { get; init; }

    public required List<DownloadCount> DownloadCounts { get; init; }

    public int LatestDownloadCount => DownloadCounts.Max(x => x.Count);
}

public class DownloadCountService(DatabaseContext dbContext)
{
    public Task<List<LauncherRelease>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        return dbContext.DownloadCounts
            .GroupBy(dc => dc.Tag)
            .Select(group => new LauncherRelease()
            {
                Tag = group.Key,
                ReleaseDate = group.First().ReleaseDate,
                DownloadCounts = group.OrderBy(order => order.Timestamp).ToList()
            })
            .ToListAsync(cancellationToken);
    }
}
