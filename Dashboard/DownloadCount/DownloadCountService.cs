using Dashboard.Database;
using Dashboard.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Dashboard.DownloadCount;

public static class DownloadStatsCalculator
{
    public static DashboardStats CalculateStats(IEnumerable<LauncherRelease> groupedHistory)
    {
        long latestReleaseDownloadCount = 0;
        long totalDownloadCount = 0;
        long totalDownloadsToday = 0;
        var today = DateTime.Today;

        // List to hold the latest DownloadCount entry for each tag
        // This is equivalent to `latestCountsPerTagOverall` from the DB version
        var latestCountsPerTag = new List<DownloadCount>();

        // Calculate totalDownloadCount and populate latestCountsPerTag
        // Also find the overall latest entry for LatestReleaseDownloadCount
        DownloadCount? overallLatestEntry = null;

        foreach (var tagGroup in groupedHistory)
        {
            var latestDcForTag = tagGroup.DownloadCounts
                                         .OrderByDescending(dc => dc.Timestamp)
                                         .FirstOrDefault();

            if (latestDcForTag != null)
            {
                latestCountsPerTag.Add(latestDcForTag);
                totalDownloadCount += latestDcForTag.Count; // Sum of latest count for each tag

                // Keep track of the overall latest entry
                if (overallLatestEntry == null || latestDcForTag.ReleaseDate > overallLatestEntry.ReleaseDate)
                {
                    overallLatestEntry = latestDcForTag;
                }
            }
        }

        // Set LatestReleaseDownloadCount
        latestReleaseDownloadCount = overallLatestEntry?.Count ?? 0;


        // --- Calculation for Total Downloads Today (using in-memory data) ---
        foreach (var tagGroup in groupedHistory)
        {
            // Get all download counts for this specific tag
            var tagDownloads = tagGroup.DownloadCounts.OrderBy(dc => dc.Timestamp).ToList();

            // Get today's entries for this specific tag
            var todayEntries = tagDownloads
                .Where(dc => dc.Timestamp.Date == today)
                .ToList();

            // If there are entries for today, proceed with calculation
            if (todayEntries.Any())
            {
                // The latest count from today is always the end point
                var latestCountToday = todayEntries.OrderByDescending(dc => dc.Timestamp).First();

                // Find the count at the beginning of today or end of yesterday
                var baselineCountEntry = tagDownloads
                    .Where(dc => dc.Timestamp.Date < today) // Get entries from before today
                    .OrderByDescending(dc => dc.Timestamp) // Find the latest from yesterday or earlier
                    .FirstOrDefault();

                long startOfDayCount;

                if (baselineCountEntry != null)
                {
                    // Found an entry from yesterday or earlier, use its count
                    startOfDayCount = baselineCountEntry.Count;
                }
                else
                {
                    // No entry from yesterday or earlier found.
                    // This means the tag is new today, or data prior to today is missing.
                    // In this case, take the EARLIEST count from today as the baseline.
                    startOfDayCount = todayEntries.OrderBy(dc => dc.Timestamp).First().Count;
                }

                // Ensure we don't subtract a larger baseline from a smaller current count (shouldn't happen with cumulative, but good for robustness)
                totalDownloadsToday += Math.Max(0, latestCountToday.Count - startOfDayCount);
            }
        }

        return new DashboardStats
        {
            LatestReleaseDownloadCount = latestReleaseDownloadCount,
            TotalDownloadCount = totalDownloadCount,
            TotalDownloadsToday = totalDownloadsToday
        };
    }
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

    // New method to get dashboard statistics
    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        // 1. Get the latest download count for each unique tag
        var latestCountsPerTag = await dbContext.DownloadCounts
            .GroupBy(dc => dc.Tag)
            .Select(g => g.OrderByDescending(dc => dc.Timestamp).FirstOrDefault())
            //.Where(dc => dc != null) // Filter out any nulls if a group was empty (unlikely with GroupBy)
            .ToListAsync(cancellationToken);

        // 2. Calculate Total Download Count (sum of latest count for each tag)
        var totalDownloadCount = latestCountsPerTag.Sum(dc => dc.Count);

        // 3. Calculate Total Downloads Today (sum of latest count for each tag, only if that latest count was recorded today)
        var today = DateTime.Today;
        var totalDownloadsToday = latestCountsPerTag
            .Where(dc => dc.Timestamp.Date == today)
            .Sum(dc => dc.Count);

        // 4. Latest Release Download Count (This definition might still need clarification,
        //    but let's assume it's the highest 'latest count' among all tags, or the count of the tag with the most recent update)
        //    Let's go with the latest count of the tag that had the absolute most recent update.
        var latestReleaseCount = latestCountsPerTag.OrderByDescending(dc => dc.ReleaseDate).FirstOrDefault();
        var latestReleaseDownloadCount = latestReleaseCount?.Count ?? 0;


        return new DashboardStats
        {
            LatestReleaseDownloadCount = latestReleaseDownloadCount,
            TotalDownloadCount = totalDownloadCount,
            TotalDownloadsToday = totalDownloadsToday
        };
    }

    /// <summary>
    /// Imports DownloadCount entities from a CSV stream.
    /// </summary>
    /// <param name="csvStream">The stream containing the CSV data.</param>
    /// <param name="dbContext">Your EF Core DbContext instance.</param>
    /// <param name="hasHeaderRow">True if the CSV file has a header row to skip, false otherwise.</param>
    /// <param name="delimiter">The character used to separate values in the CSV. Defaults to comma.</param>
    /// <param name="dateFormat">The format string for parsing DateTime values. Defaults to "yyyy-MM-dd HH:mm:ss" for Timestamp and "yyyy-MM-dd" for ReleaseDate.</param>
    /// <param name="batchSize">The number of entities to add to the context before saving changes. Defaults to 100.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <exception cref="FormatException">Thrown if a CSV line cannot be parsed correctly.</exception>
    /// <exception cref="IOException">Thrown if there are issues reading from the stream.</exception>
    public async Task ImportDownloadCountsFromCsvAsync(
        string tagName,
        DateTime releaseDate,
        Stream csvStream,
        bool hasHeaderRow = true,        
        char delimiter = ',',
        int batchSize = 100)
    {
        using var reader = new StreamReader(csvStream);
        var downloadCountsToAdd = new List<DownloadCount>();
        int lineNumber = 0;

        if (hasHeaderRow)
        {
            await reader.ReadLineAsync(); // Skip the header row
            lineNumber++;
        }

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // Skip empty lines
            }

            var parts = line.Split(delimiter);

            // Expecting 2 columns: Timestamp, Count
            if (parts.Length != 2)
            {
                throw new FormatException($"CSV line {lineNumber} has an incorrect number of columns. Expected 2, got {parts.Length}. Line: \"{line}\"");
            }

            try
            {
                // Id is typically auto-generated by the database, so we might not import it from CSV.
                // If your CSV includes an Id and you want to use it, uncomment the following line
                // and ensure your database allows explicit Id insertion (e.g., not IDENTITY).
                // For this example, we'll let the database generate Id.

                var downloadCount = new DownloadCount
                {
                    // Id = int.Parse(parts[0].Trim()), // Only if importing Id from CSV and database allows
                    Tag = tagName,
                    Timestamp = DateTime.SpecifyKind(DateTime.Parse(parts[0].Trim()), DateTimeKind.Utc),
                    Count = int.Parse(parts[1].Trim()),
                    ReleaseDate = releaseDate,
                };

                downloadCountsToAdd.Add(downloadCount);

                if (downloadCountsToAdd.Count >= batchSize)
                {
                    await dbContext.DownloadCounts.AddRangeAsync(downloadCountsToAdd);
                    await dbContext.SaveChangesAsync();
                    downloadCountsToAdd.Clear();
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Error parsing CSV line {lineNumber}: \"{line}\". Details: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // Catch any other unexpected errors during parsing or entity creation
                throw new InvalidOperationException($"An unexpected error occurred processing CSV line {lineNumber}: \"{line}\". Details: {ex.Message}", ex);
            }
        }

        // Add any remaining entities
        if (downloadCountsToAdd.Any())
        {
            await dbContext.DownloadCounts.AddRangeAsync(downloadCountsToAdd);
            await dbContext.SaveChangesAsync();
        }
    }
}
