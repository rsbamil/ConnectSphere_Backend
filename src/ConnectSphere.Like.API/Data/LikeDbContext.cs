using Microsoft.EntityFrameworkCore;
using ConnectSphere.Like.Models;

namespace ConnectSphere.Like.Data;

public class LikeDbContext : DbContext
{
    public LikeDbContext(DbContextOptions<LikeDbContext> options) : base(options) { }

    public DbSet<Models.Like> Likes => Set<Models.Like>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Models.Like>(entity =>
        {
            // Prevent duplicate likes at DB level
            entity.HasIndex(l => new { l.UserId, l.TargetId, l.TargetType }).IsUnique();
            entity.Property(l => l.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}