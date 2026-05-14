using Microsoft.EntityFrameworkCore;
using ConnectSphere.Post.Models;

namespace ConnectSphere.Post.Data;

public class PostDbContext : DbContext
{
    public PostDbContext(DbContextOptions<PostDbContext> options) : base(options) { }

    public DbSet<Models.Post> Posts => Set<Models.Post>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Models.Post>(entity =>
        {
            entity.HasIndex(p => new { p.UserId, p.CreatedAt });
            entity.HasIndex(p => new { p.Visibility, p.IsDeleted });
            entity.HasIndex(p => p.CreatedAt);
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}