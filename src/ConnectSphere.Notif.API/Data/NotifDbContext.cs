using Microsoft.EntityFrameworkCore;
using ConnectSphere.Notif.Models;

namespace ConnectSphere.Notif.Data;

public class NotifDbContext : DbContext
{
    public NotifDbContext(DbContextOptions<NotifDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => new { n.RecipientId, n.IsRead });
            entity.HasIndex(n => new { n.RecipientId, n.CreatedAt });
            entity.Property(n => n.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}