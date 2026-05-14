using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Auth.Models;

/// <summary>
/// Core user entity. Stored in the auth_users table.
/// FollowerCount, FollowingCount, PostCount are denormalised here
/// and updated atomically via ExecuteUpdateAsync — avoids COUNT(*) on every profile load.
/// </summary>
[Table("auth_users")]
[Index(nameof(UserName), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public int UserId { get; set; }

    [Required, MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    // PBKDF2 hash via ASP.NET Core PasswordHasher<User>
    public string? PasswordHash { get; set; }

    [MaxLength(100)]
    public string? GoogleId { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    // Azure Blob SAS URL or null if no avatar uploaded
    public string? AvatarUrl { get; set; }

    // When true, follow requests go PENDING until approved
    public bool IsPrivate { get; set; } = false;

    // Admins can set to false to suspend the account
    public bool IsActive { get; set; } = true;

    // Admin role flag
    public bool IsAdmin { get; set; } = false;

    // Denormalised counters — updated via ExecuteUpdateAsync
    public int FollowerCount { get; set; } = 0;
    public int FollowingCount { get; set; } = 0;
    public int PostCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation helper methods
    public string GetDisplayName() => FullName.Length > 0 ? FullName : UserName;
    public bool IsEqual(int userId) => UserId == userId;
    public override string ToString() => $"@{UserName} ({UserId})";
}