namespace Dashboard.Database.Entities;

public class DownloadCount
{
    public int Id { get; set; }
    public required string Tag { get; set; }
    public DateTime Timestamp { get; set; }
    public int Count { get; set; }
    public DateTime ReleaseDate { get; set; }
}
