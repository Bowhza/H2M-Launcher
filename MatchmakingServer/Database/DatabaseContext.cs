using MatchmakingServer.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace MatchmakingServer.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<UserDbo> Users => Set<UserDbo>();

    public DbSet<UserKeyDbo> UserKeys => Set<UserKeyDbo>();

    public DbSet<FriendshipDbo> UserFriendships => Set<FriendshipDbo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserDbo>(entity =>
        {
            entity.HasMany(u => u.Keys)
                  .WithOne(k => k.User)
                  .HasForeignKey(k => k.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(u => u.Name)
                  .IsUnique();
        });

            entity.HasIndex(u => u.Name)
                  .IsUnique();
        });
            

        modelBuilder.Entity<UserKeyDbo>()
            .HasIndex(k => k.PublicKeySPKI)
            .IsUnique();

        modelBuilder.Entity<FriendshipDbo>(entity =>
        {
            entity.HasKey(ur => new { ur.FromUserId, ur.ToUserId });

            entity.HasOne(ur => ur.FromUser)
                  .WithMany()
                  .HasForeignKey(ur => ur.FromUserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.ToUser)
                  .WithMany()
                  .HasForeignKey(ur => ur.ToUserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(ur => ur.Status)
                  .HasConversion<string>()
                  .IsRequired();
        });
    }
}
