using Dashboard.Database.Entities;
using Dashboard.Party;

using Microsoft.EntityFrameworkCore;

namespace Dashboard.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<DownloadCount> DownloadCounts => Set<DownloadCount>();

    public DbSet<PartySnapshot> PartySnapshots => Set<PartySnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DownloadCount>((e) =>
        {
            e.HasIndex(dc => dc.Tag);
            e.HasIndex(dc => dc.Timestamp);
        });

        modelBuilder.Entity<PartySnapshot>((e) =>
        {
            e.HasIndex(dc => dc.PartyId);
            e.HasIndex(dc => dc.Timestamp);

            e.HasIndex(ps => new { ps.PartyId, ps.Timestamp })
             .IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
