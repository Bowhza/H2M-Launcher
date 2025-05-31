namespace Dashboard.GitHub;

using Refit;

using System.Collections.Generic;
using System.Threading.Tasks;

public interface IGitHubApiClient
{
    [Headers("User-Agent: RefitGitHubApiClient", "Accept: application/vnd.github.v3+json")]
    [Get("/repos/{owner}/{repo}/releases")]
    Task<IReadOnlyList<GitHubRelease>> GetRepositoryReleasesAsync(string owner, string repo);

    [Headers("User-Agent: RefitGitHubApiClient", "Accept: application/vnd.github.v3+json")]
    [Get("/repos/{owner}/{repo}/releases/tags/{tag}")]
    Task<GitHubRelease> GetReleaseByTagNameAsync(string owner, string repo, string tag);
}
