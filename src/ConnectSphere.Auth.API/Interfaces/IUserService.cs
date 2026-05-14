using ConnectSphere.Auth.DTOs;
using ConnectSphere.Auth.Models;

namespace ConnectSphere.Auth.Interfaces;

/// <summary>
/// Business logic layer for user management and authentication.
/// Injected into controllers via constructor DI.
/// </summary>
public interface IUserService
{
    // ── Authentication ──────────────────────────────────────────────────────
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> GoogleLoginAsync(string idToken);
    Task<bool> ValidateTokenAsync(string token);

    // ── Profile management ──────────────────────────────────────────────────
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<UserDto?> GetUserByUserNameAsync(string userName);
    Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request);

    /// <summary>
    /// Flips IsPrivate bool via ExecuteUpdateAsync.
    /// Returns the new value.
    /// </summary>
    Task<bool> TogglePrivacyAsync(int userId);

    // ── Search & Discovery ─────────────────────────────────────────────────
    Task<IList<UserSummaryDto>> SearchUsersAsync(string query, int page = 1, int pageSize = 20);

    /// <summary>
    /// Returns users not yet followed by userId, sorted by mutual followers count.
    /// </summary>
    Task<IList<UserSummaryDto>> GetSuggestedUsersAsync(int userId);

    // ── Counter management (called by other services) ──────────────────────
    Task UpdateCountersAsync(int userId, string field, int delta);

    // ── Admin operations ────────────────────────────────────────────────────
    Task<IList<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 20);
    Task DeactivateAccountAsync(int userId, int adminId);
    Task DeleteAccountAsync(int userId, int adminId);
}