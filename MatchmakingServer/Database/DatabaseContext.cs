using MatchmakingServer.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace MatchmakingServer.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<UserDbo> Users => Set<UserDbo>();

    public DbSet<UserKeyDbo> UserKeys => Set<UserKeyDbo>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserDbo>()
            .HasMany<UserKeyDbo>(u => u.Keys)
            .WithOne(k => k.User)
            .HasForeignKey(k => k.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserKeyDbo>()
            .HasIndex(k => k.PublicKeySPKI)
            .IsUnique();
    }
}
