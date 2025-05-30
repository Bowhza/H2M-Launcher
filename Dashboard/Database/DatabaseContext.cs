using Dashboard.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Dashboard.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<DownloadCount> DownloadCounts => Set<DownloadCount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DownloadCount>((e) =>
        {
            e.HasIndex(dc => dc.Tag);
            e.HasIndex(dc => dc.Timestamp);
        });

        base.OnModelCreating(modelBuilder);
    }
}
