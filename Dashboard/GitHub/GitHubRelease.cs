namespace Dashboard.GitHub;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class GitHubRelease
{
    public long Id { get; set; }

    [JsonPropertyName("tag_name")]
    public required string TagName { get; set; }
    public required string Name { get; set; }
    public IReadOnlyList<GitHubReleaseAsset> Assets { get; set; } = [];
    public bool Draft { get; set; } // To filter out draft releases
    public bool Prerelease { get; set; } // To filter out pre-releases

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
