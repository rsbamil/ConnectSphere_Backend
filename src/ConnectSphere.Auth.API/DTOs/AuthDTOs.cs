using System.ComponentModel.DataAnnotations;

namespace ConnectSphere.Auth.DTOs;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class RegisterRequest
{
    [Required, MinLength(3), MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int PostCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserSummaryDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsPrivate { get; set; }
    public int FollowerCount { get; set; }
    public int MutualFollowersCount { get; set; }
}

public class UpdateCounterRequest
{
    [Required]
    public string Field { get; set; } = string.Empty; // "FollowerCount" | "FollowingCount" | "PostCount"

    [Required]
    public int Delta { get; set; } // +1 or -1
}