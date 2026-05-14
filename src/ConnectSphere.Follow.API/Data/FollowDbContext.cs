using Microsoft.EntityFrameworkCore;
using ConnectSphere.Follow.Models;

namespace ConnectSphere.Follow.Data;

public class FollowDbContext : DbContext
{
    public FollowDbContext(DbContextOptions<FollowDbContext> options) : base(options) { }

    public DbSet<Models.Follow> Follows => Set<Models.Follow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Models.Follow>(entity =>
        {
            entity.HasIndex(f => new { f.FollowerId, f.FolloweeId }).IsUnique();
            entity.HasIndex(f => f.FolloweeId);
            entity.HasIndex(f => f.FollowerId);
            entity.Property(f => f.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}