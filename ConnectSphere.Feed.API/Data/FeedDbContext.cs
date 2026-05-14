using Microsoft.EntityFrameworkCore;
using ConnectSphere.Feed.Models;

namespace ConnectSphere.Feed.Data;

public class FeedDbContext : DbContext
{
    public FeedDbContext(DbContextOptions<FeedDbContext> options) : base(options) { }

    public DbSet<FeedItem> FeedItems => Set<FeedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FeedItem>(entity =>
        {
            entity.HasIndex(f => new { f.UserId, f.CreatedAt });
            entity.Property(f => f.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}