using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Auth.DTOs;
using ConnectSphere.Auth.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Auth.Controllers;

/// <summary>
/// REST endpoints for user management and authentication.
/// Route: /api/users
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    // ── Auth endpoints ──────────────────────────────────────────────────────

    /// <summary>Register a new account. Returns JWT on success.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _userService.RegisterAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message ,
            inner = ex.InnerException?.Message
            });
        }
    }

    /// <summary>Login with username/email + password. Returns JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _userService.LoginAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>Login or Register using a Google ID token.</summary>
    [HttpPost("google-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var result = await _userService.GoogleLoginAsync(request.IdToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ── Profile endpoints ───────────────────────────────────────────────────

    /// <summary>Get public profile by user ID.</summary>
    [HttpGet("{userId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Get public profile by username.</summary>
    [HttpGet("by-username/{userName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByUserName(string userName)
    {
        var user = await _userService.GetUserByUserNameAsync(userName);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Search users by username or full name.</summary>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var results = await _userService.SearchUsersAsync(q, page, pageSize);
        return Ok(results);
    }

    /// <summary>Update the authenticated user's profile.</summary>
    [HttpPut("{userId:int}/profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(int userId, [FromBody] UpdateProfileRequest request)
    {
        if (!IsOwnerOrAdmin(userId))
            return Forbid();

        var updated = await _userService.UpdateProfileAsync(userId, request);
        return Ok(updated);
    }

    /// <summary>Change password.</summary>
    [HttpPut("{userId:int}/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequest request)
    {
        if (!IsOwnerOrAdmin(userId))
            return Forbid();
        try
        {
            await _userService.ChangePasswordAsync(userId, request);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Toggle account privacy (public ↔ private).</summary>
    [HttpPut("{userId:int}/toggle-privacy")]
    [Authorize]
    public async Task<IActionResult> TogglePrivacy(int userId)
    {
        if (!IsOwnerOrAdmin(userId))
            return Forbid();

        var isPrivate = await _userService.TogglePrivacyAsync(userId);
        return Ok(new { isPrivate });
    }

    /// <summary>Get suggested users to follow.</summary>
    [HttpGet("{userId:int}/suggested")]
    [Authorize]
    public async Task<IActionResult> GetSuggested(int userId)
    {
        var suggestions = await _userService.GetSuggestedUsersAsync(userId);
        return Ok(suggestions);
    }

    // ── Internal endpoint (called by other services) ─────────────────────────

    /// <summary>
    /// Atomically update a counter field on a user.
    /// Called by Follow-Service and Post-Service via HTTP.
    /// Requires a valid service-to-service JWT.
    /// </summary>
    [HttpPut("{userId:int}/counters")]
    [Authorize]
    public async Task<IActionResult> UpdateCounters(int userId, [FromBody] UpdateCounterRequest request)
    {
        try
        {
            await _userService.UpdateCountersAsync(userId, request.Field, request.Delta);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Admin endpoints ─────────────────────────────────────────────────────

    /// <summary>Admin: list all users with pagination.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var users = await _userService.GetAllUsersAsync(page, pageSize);
        return Ok(users);
    }

    /// <summary>Admin: suspend a user account (IsActive = false).</summary>
    [HttpPut("{userId:int}/suspend")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Suspend(int userId)
    {
        var adminId = GetCurrentUserId();
        await _userService.DeactivateAccountAsync(userId, adminId);
        return NoContent();
    }

    /// <summary>Admin: permanently delete a user account.</summary>
    [HttpDelete("{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int userId)
    {
        var adminId = GetCurrentUserId();
        await _userService.DeleteAccountAsync(userId, adminId);
        return NoContent();
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    private bool IsOwnerOrAdmin(int userId)
    {
        if (User.IsInRole("Admin")) return true;
        return GetCurrentUserId() == userId;
    }
}