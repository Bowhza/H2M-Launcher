using Dashboard.Database.Entities;

namespace Dashboard.DownloadCount;

public class LauncherRelease
{
    public required string Tag { get; init; }

    public required DateTime ReleaseDate { get; init; }

    public required List<DownloadCount> DownloadCounts { get; init; }

    public int LatestDownloadCount => DownloadCounts.Max(x => x.Count);
}
