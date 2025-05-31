namespace Dashboard.GitHub;
using System.Text.Json.Serialization;

public class GitHubReleaseAsset
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Label { get; set; }

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("browser_download_url")]
    public required string BrowserDownloadUrl { get; set; }
}
