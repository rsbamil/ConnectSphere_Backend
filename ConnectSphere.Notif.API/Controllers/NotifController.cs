using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Notif.DTOs;
using ConnectSphere.Notif.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Notif.Controllers;

/// <summary>
/// REST endpoints for notifications.
/// Route: /api/notifications
///
/// The /like, /comment, /follow endpoints are called by other services
/// with a valid JWT (service-to-service communication).
/// </summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotifController : ControllerBase
{
    private readonly INotifService _notifService;

    public NotifController(INotifService notifService)
    {
        _notifService = notifService;
    }

    // ── Inbound from other services ─────────────────────────────────────────

    /// <summary>Called by Like service after a successful like toggle.</summary>
    [HttpPost("like")]
    [Authorize]
    public async Task<IActionResult> LikeNotif([FromBody] LikeNotifRequest request)
    {
        await _notifService.SendLikeNotifAsync(request);
        return NoContent();
    }

    /// <summary>Called by Comment service after a comment/reply is added.</summary>
    [HttpPost("comment")]
    [Authorize]
    public async Task<IActionResult> CommentNotif([FromBody] CommentNotifRequest request)
    {
        await _notifService.SendCommentNotifAsync(request);
        return NoContent();
    }

    /// <summary>Called by Follow service on follow/accept events.</summary>
    [HttpPost("follow")]
    [Authorize]
    public async Task<IActionResult> FollowNotif([FromBody] FollowNotifRequest request)
    {
        await _notifService.SendFollowNotifAsync(request);
        return NoContent();
    }

    // ── User-facing endpoints ───────────────────────────────────────────────

    /// <summary>Get paginated notifications for the current user.</summary>
    [HttpGet("{userId:int}")]
    [Authorize]
    public async Task<IActionResult> GetByRecipient(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        return Ok(await _notifService.GetByRecipientAsync(userId, page, pageSize));
    }

    /// <summary>Get unread notifications.</summary>
    [HttpGet("{userId:int}/unread")]
    [Authorize]
    public async Task<IActionResult> GetUnread(int userId)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        return Ok(await _notifService.GetUnreadAsync(userId));
    }

    /// <summary>Get unread notification count (for the badge).</summary>
    [HttpGet("{userId:int}/unread-count")]
    [Authorize]
    public async Task<IActionResult> GetUnreadCount(int userId)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        var count = await _notifService.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    /// <summary>Mark a single notification as read.</summary>
    [HttpPut("{notificationId:int}/read")]
    [Authorize]
    public async Task<IActionResult> MarkRead(int notificationId)
    {
        var userId = GetCurrentUserId();
        await _notifService.MarkAsReadAsync(notificationId, userId);
        return NoContent();
    }

    /// <summary>
    /// Mark ALL notifications as read for the current user.
    /// Uses EF Core ExecuteUpdateAsync batch update — no entity loading.
    /// </summary>
    [HttpPut("{userId:int}/read-all")]
    [Authorize]
    public async Task<IActionResult> MarkAllRead(int userId)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        await _notifService.MarkAllReadAsync(userId);
        return NoContent();
    }

    /// <summary>Delete a notification.</summary>
    [HttpDelete("{notificationId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int notificationId)
    {
        var userId = GetCurrentUserId();
        await _notifService.DeleteNotifAsync(notificationId, userId);
        return NoContent();
    }

    // ── Admin ───────────────────────────────────────────────────────────────

    /// <summary>Admin: send a platform-wide broadcast notification.</summary>
    [HttpPost("broadcast")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest request)
    {
        await _notifService.SendBulkAsync(request);
        return Ok(new { message = $"Broadcast sent to {request.UserIds.Count} users." });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    private bool IsOwnerOrAdmin(int userId)
        => User.IsInRole("Admin") || GetCurrentUserId() == userId;
}