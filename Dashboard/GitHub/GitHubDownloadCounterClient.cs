namespace Dashboard.GitHub;

using Refit;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class GitHubDownloadCounterClient
{
    private readonly IGitHubApiClient _gitHubApiClient;

    public GitHubDownloadCounterClient(IGitHubApiClient gitHubApiClient)
    {
        _gitHubApiClient = gitHubApiClient;
    }

    /// <summary>
    /// Fetches the total download count for all assets across all *published* releases in a given repository.
    /// Draft and pre-release assets are excluded by default.
    /// </summary>
    /// <param name="owner">The owner of the repository (e.g., "Bowhza").</param>
    /// <param name="repo">The name of the repository (e.g., "H2M-Launcher").</param>
    /// <returns>A dictionary where the key is the release tag name and the value is its total download count.</returns>
    public async Task<Dictionary<string, int>> GetDownloadCountsForAllReleasesAsync(string owner, string repo)
    {
        var releaseDownloadCounts = new Dictionary<string, int>();

        try
        {
            // GitHub's /releases endpoint returns all releases, including drafts and pre-releases.
            // We'll filter them out if we only want "published" releases.
            var releases = await _gitHubApiClient.GetRepositoryReleasesAsync(owner, repo);

            foreach (var release in releases)
            {
                // Exclude draft and pre-releases by default, as these typically don't count towards official downloads.
                // Adjust this logic if you want to include them.
                if (release.Draft || release.Prerelease)
                {
                    Console.WriteLine($"Skipping draft or pre-release: {release.TagName}");
                    continue;
                }

                int totalDownloadsForRelease = 0;
                if (release.Assets != null)
                {
                    foreach (var asset in release.Assets)
                    {
                        totalDownloadsForRelease += asset.DownloadCount;
                    }
                }
                releaseDownloadCounts[release.TagName] = totalDownloadsForRelease;
            }
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"GitHub API Error: {ex.StatusCode} - {ex.Content}");
            throw; // Re-throw the exception after logging
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            throw; // Re-throw the exception
        }

        return releaseDownloadCounts;
    }
}
