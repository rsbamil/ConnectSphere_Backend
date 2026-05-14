using Microsoft.EntityFrameworkCore;
using ConnectSphere.Notif.Data;
using ConnectSphere.Notif.DTOs;
using ConnectSphere.Notif.Interfaces;
using ConnectSphere.Notif.Models;

namespace ConnectSphere.Notif.Services;

/// <summary>
/// Creates and manages in-app notifications.
/// Each SendXxxNotif() constructs a Notification entity and saves it.
/// MarkAllRead() uses EF Core ExecuteUpdateAsync for a single batch SQL UPDATE
/// — no entity hydration needed.
/// SendBulk() creates one Notification record per userId in batch for admin broadcasts.
/// </summary>
public class NotifService : INotifService
{
    private readonly NotifDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<NotifService> _logger;

    public NotifService(NotifDbContext db, IHttpClientFactory httpFactory, ILogger<NotifService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // ── Typed helpers ───────────────────────────────────────────────────────

    public async Task SendLikeNotifAsync(LikeNotifRequest req)
    {
        // Resolve the post/comment owner's userId
        int recipientId = await ResolveOwnerAsync(req.TargetId, req.TargetType);
        if (recipientId == 0 || recipientId == req.ActorId) return; // Don't notify yourself

        await SaveNotificationAsync(new Notification
        {
            RecipientId = recipientId,
            ActorId = req.ActorId,
            Type = req.Type,
            TargetId = req.TargetId,
            TargetType = req.TargetType,
            Message = $"Someone liked your {req.TargetType.ToLower()}."
        });
    }

    public async Task SendCommentNotifAsync(CommentNotifRequest req)
    {
        // Resolve post owner
        int recipientId = await ResolveOwnerAsync(req.PostId, "POST");
        if (recipientId == 0 || recipientId == req.ActorId) return;

        await SaveNotificationAsync(new Notification
        {
            RecipientId = recipientId,
            ActorId = req.ActorId,
            Type = req.Type,
            TargetId = req.PostId,
            TargetType = "POST",
            Message = req.Type == "NEW_REPLY"
                ? "Someone replied to your comment."
                : "Someone commented on your post."
        });
    }

    public async Task SendFollowNotifAsync(FollowNotifRequest req)
    {
        if (req.ActorId == req.RecipientId) return;

        var message = req.Type switch
        {
            "FOLLOW_REQUEST" => "Someone sent you a follow request.",
            "NEW_FOLLOWER" => "Someone started following you.",
            "FOLLOW_ACCEPTED" => "Your follow request was accepted.",
            _ => "Follow activity."
        };

        await SaveNotificationAsync(new Notification
        {
            RecipientId = req.RecipientId,
            ActorId = req.ActorId,
            Type = req.Type,
            TargetId = req.ActorId,
            TargetType = "USER",
            Message = message
        });
    }

    // ── Fetch ───────────────────────────────────────────────────────────────

    public async Task<IList<NotificationDto>> GetByRecipientAsync(int userId, int page = 1, int pageSize = 20)
        => await _db.Notifications
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => MapToDto(n))
            .ToListAsync();

    public async Task<IList<NotificationDto>> GetUnreadAsync(int userId)
        => await _db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => MapToDto(n))
            .ToListAsync();

    public async Task<int> GetUnreadCountAsync(int userId)
        => await _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);

    // ── Mark read ───────────────────────────────────────────────────────────

    public async Task MarkAsReadAsync(int notificationId, int userId)
        => await _db.Notifications
            .Where(n => n.NotificationId == notificationId && n.RecipientId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    /// <summary>
    /// Single SQL UPDATE — sets IsRead = true for all unread notifications.
    /// Uses ExecuteUpdateAsync so no entities are loaded into memory.
    /// </summary>
    public async Task MarkAllReadAsync(int userId)
        => await _db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public async Task DeleteNotifAsync(int notificationId, int userId)
        => await _db.Notifications
            .Where(n => n.NotificationId == notificationId && n.RecipientId == userId)
            .ExecuteDeleteAsync();

    // ── Admin broadcast ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates one PLATFORM Notification per userId in a single SaveChangesAsync call.
    /// More efficient than calling SaveChangesAsync inside the loop.
    /// </summary>
    public async Task SendBulkAsync(BroadcastRequest req)
    {
        var notifications = req.UserIds.Select(uid => new Notification
        {
            RecipientId = uid,
            ActorId = null,
            Type = "PLATFORM",
            Message = $"{req.Title}: {req.Message}",
            TargetType = null,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Platform broadcast sent to {Count} users", notifications.Count);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task SaveNotificationAsync(Notification notification)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification [{Type}] → recipient {RecipientId}", notification.Type, notification.RecipientId);
    }

    /// <summary>
    /// Resolves the owner (userId) of a post or comment by calling the appropriate service.
    /// Returns 0 if the lookup fails so the caller can skip gracefully.
    /// </summary>
    private async Task<int> ResolveOwnerAsync(int targetId, string targetType)
    {
        try
        {
            var clientName = targetType.ToUpper() == "POST" ? "PostService" : "CommentService";
            var path = targetType.ToUpper() == "POST"
                ? $"/api/posts/{targetId}"
                : $"/api/comments/{targetId}";

            var client = _httpFactory.CreateClient(clientName);
            var result = await client.GetFromJsonAsync<OwnerResponse>(path);
            return result?.UserId ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve owner for {TargetType} {TargetId}", targetType, targetId);
            return 0;
        }
    }

    private static NotificationDto MapToDto(Notification n) => new()
    {
        NotificationId = n.NotificationId,
        RecipientId = n.RecipientId,
        ActorId = n.ActorId,
        Type = n.Type,
        Message = n.Message,
        TargetId = n.TargetId,
        TargetType = n.TargetType,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
    };

    private record OwnerResponse(int UserId);
}