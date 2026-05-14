using Microsoft.EntityFrameworkCore;
using ConnectSphere.Auth.Models;

namespace ConnectSphere.Auth.Data;

/// <summary>
/// EF Core DbContext for the Auth/User service.
/// Connects to Neon (PostgreSQL) via the DATABASE_URL environment variable.
/// </summary>
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.UserName).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.IsActive); // Fast query for active users
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.ActorId);
            entity.HasIndex(a => a.CreatedAt);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}