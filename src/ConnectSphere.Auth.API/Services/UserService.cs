using System.IdentityModel.Tokens.Jwt;
using Google.Apis.Auth;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ConnectSphere.Auth.DTOs;
using ConnectSphere.Auth.Interfaces;
using ConnectSphere.Auth.Models;

namespace ConnectSphere.Auth.Services;

/// <summary>
/// Implements IUserService — handles registration, login, profile management,
/// and counter updates called by other microservices via HTTP.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IConfiguration _config;
    private readonly ILogger<UserService> _logger;
    private readonly PasswordHasher<User> _hasher = new();

    public UserService(IUserRepository repo, IConfiguration config, ILogger<UserService> logger)
    {
        _repo = repo;
        _config = config;
        _logger = logger;
    }

    // ── Authentication ──────────────────────────────────────────────────────

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        if (await _repo.ExistsByUserNameAsync(req.UserName))
            throw new InvalidOperationException($"Username '{req.UserName}' is already taken.");

        if (await _repo.ExistsByEmailAsync(req.Email))
            throw new InvalidOperationException($"Email '{req.Email}' is already registered.");

        var user = new User
        {
            UserName = req.UserName.Trim(),
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim().ToLower(),
            CreatedAt = DateTime.UtcNow
        };

        // Hash password with PBKDF2 via ASP.NET Core PasswordHasher
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        await _repo.CreateAsync(user);
        _logger.LogInformation("New user registered: {UserName} (ID={UserId})", user.UserName, user.UserId);

        return new AuthResponse
        {
            Token = GenerateJwt(user),
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        // Find by email or username
        var user = req.UserNameOrEmail.Contains('@')
            ? await _repo.FindByEmailAsync(req.UserNameOrEmail)
            : await _repo.FindByUserNameAsync(req.UserNameOrEmail);

        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid credentials or account suspended.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash!, req.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid credentials.");

        _logger.LogInformation("User logged in: {UserName}", user.UserName);

        return new AuthResponse
        {
            Token = GenerateJwt(user),
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponse> GoogleLoginAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _config["Google:ClientId"] }
        };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogError(ex, "Invalid Google ID Token.");
            throw new UnauthorizedAccessException("Invalid Google token.");
        }

        // 1. Check if user with this GoogleId exists
        var user = await _repo.FindByGoogleIdAsync(payload.Subject);

        // 2. If not, check if user with this email exists
        if (user == null)
        {
            user = await _repo.FindByEmailAsync(payload.Email);
            if (user != null)
            {
                // Link GoogleId to existing account
                user.GoogleId = payload.Subject;
                await _repo.UpdateAsync(user);
            }
        }

        // 3. If still no user, create a new one
        if (user == null)
        {
            // Extract a username from email or name
            var baseUserName = payload.Email.Split('@')[0];
            var userName = baseUserName;
            int suffix = 1;
            while (await _repo.ExistsByUserNameAsync(userName))
            {
                userName = $"{baseUserName}{suffix++}";
            }

            user = new User
            {
                UserName = userName,
                FullName = payload.Name ?? userName,
                Email = payload.Email.ToLower(),
                GoogleId = payload.Subject,
                AvatarUrl = payload.Picture,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.CreateAsync(user);
            _logger.LogInformation("New user created via Google login: {UserName} (ID={UserId})", user.UserName, user.UserId);
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account suspended.");

        return new AuthResponse
        {
            Token = GenerateJwt(user),
            User = MapToDto(user)
        };
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            handler.ValidateToken(token, GetValidationParams(), out _);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    // ── Profile management ──────────────────────────────────────────────────

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _repo.FindByUserIdAsync(userId);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto?> GetUserByUserNameAsync(string userName)
    {
        var user = await _repo.FindByUserNameAsync(userName);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest req)
    {
        var user = await _repo.FindByUserIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (req.FullName is not null) user.FullName = req.FullName.Trim();
        if (req.Bio is not null) user.Bio = req.Bio.Trim();
        if (req.AvatarUrl is not null) user.AvatarUrl = req.AvatarUrl;

        var updated = await _repo.UpdateAsync(user);
        return MapToDto(updated);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest req)
    {
        var user = await _repo.FindByUserIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash!, req.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        await _repo.UpdateAsync(user);
        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public async Task<bool> TogglePrivacyAsync(int userId)
    {
        var user = await _repo.FindByUserIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var newValue = !user.IsPrivate;
        user.IsPrivate = newValue;
        await _repo.UpdateAsync(user);
        return newValue;
    }

    // ── Search & Discovery ──────────────────────────────────────────────────

    public async Task<IList<UserSummaryDto>> SearchUsersAsync(string query, int page = 1, int pageSize = 20)
    {
        var users = await _repo.SearchUsersAsync(query, page, pageSize);
        return users.Select(u => new UserSummaryDto
        {
            UserId = u.UserId,
            UserName = u.UserName,
            FullName = u.FullName,
            AvatarUrl = u.AvatarUrl,
            IsPrivate = u.IsPrivate,
            FollowerCount = u.FollowerCount
        }).ToList();
    }

    public async Task<IList<UserSummaryDto>> GetSuggestedUsersAsync(int userId)
    {
        // Returns active users sorted by follower count (excluding the requesting user).
        // In a full implementation this would intersect follower sets for mutual count.
        // Here we return top accounts not equal to current user as a sensible default.
        var all = await _repo.FindAllActiveAsync(1, 50);
        return all
            .Where(u => u.UserId != userId)
            .OrderByDescending(u => u.FollowerCount)
            .Take(10)
            .Select(u => new UserSummaryDto
            {
                UserId = u.UserId,
                UserName = u.UserName,
                FullName = u.FullName,
                AvatarUrl = u.AvatarUrl,
                IsPrivate = u.IsPrivate,
                FollowerCount = u.FollowerCount
            }).ToList();
    }

    // ── Counter management ─────────────────────────────────────────────────

    public async Task UpdateCountersAsync(int userId, string field, int delta)
        => await _repo.UpdateCountersAsync(userId, field, delta);

    // ── Admin operations ────────────────────────────────────────────────────

    public async Task<IList<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 20)
    {
        var users = await _repo.FindAllAsync(page, pageSize);
        return users.Select(MapToDto).ToList();
    }

    public async Task DeactivateAccountAsync(int userId, int adminId)
    {
        var user = await _repo.FindByUserIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        user.IsActive = false;
        await _repo.UpdateAsync(user);
        _logger.LogWarning("User {UserId} suspended by admin {AdminId}", userId, adminId);
    }

    public async Task DeleteAccountAsync(int userId, int adminId)
    {
        await _repo.DeleteAsync(userId);
        _logger.LogWarning("User {UserId} permanently deleted by admin {AdminId}", userId, adminId);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private string GenerateJwt(User user)
    {
        var jwtSecret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT:Secret not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "ConnectSphere",
            audience: _config["Jwt:Audience"] ?? "ConnectSphere",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters GetValidationParams()
    {
        var jwtSecret = _config["Jwt:Secret"] ?? "";
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    }

    private static UserDto MapToDto(User u) => new()
    {
        UserId = u.UserId,
        UserName = u.UserName,
        FullName = u.FullName,
        Email = u.Email,
        Bio = u.Bio,
        AvatarUrl = u.AvatarUrl,
        IsPrivate = u.IsPrivate,
        IsActive = u.IsActive,
        IsAdmin = u.IsAdmin,
        FollowerCount = u.FollowerCount,
        FollowingCount = u.FollowingCount,
        PostCount = u.PostCount,
        CreatedAt = u.CreatedAt
    };
}