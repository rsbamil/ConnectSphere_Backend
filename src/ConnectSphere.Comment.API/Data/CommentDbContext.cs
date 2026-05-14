using Microsoft.EntityFrameworkCore;
using ConnectSphere.Comment.Models;

namespace ConnectSphere.Comment.Data;

public class CommentDbContext : DbContext
{
    public CommentDbContext(DbContextOptions<CommentDbContext> options) : base(options) { }

    public DbSet<Models.Comment> Comments => Set<Models.Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Models.Comment>(entity =>
        {
            entity.HasIndex(c => new { c.PostId, c.ParentCommentId });
            entity.HasIndex(c => c.UserId);
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}