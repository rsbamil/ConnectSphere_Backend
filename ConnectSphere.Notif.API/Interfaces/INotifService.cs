using ConnectSphere.Notif.DTOs;

namespace ConnectSphere.Notif.Interfaces;

/// <summary>
/// Notification business logic.
/// Each dedicated helper (SendLikeNotif, SendCommentNotif, etc.) constructs
/// the correct Notification entity and calls the internal Send() method.
/// MarkAllRead uses ExecuteUpdateAsync — no entity load required.
/// </summary>
public interface INotifService
{
    // ── Typed helpers (called by other services via HTTP) ─────────────────
    Task SendLikeNotifAsync(LikeNotifRequest request);
    Task SendCommentNotifAsync(CommentNotifRequest request);
    Task SendFollowNotifAsync(FollowNotifRequest request);

    // ── Fetch ─────────────────────────────────────────────────────────────
    Task<IList<NotificationDto>> GetByRecipientAsync(int userId, int page = 1, int pageSize = 20);
    Task<IList<NotificationDto>> GetUnreadAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);

    // ── Mark read ─────────────────────────────────────────────────────────
    Task MarkAsReadAsync(int notificationId, int userId);

    /// <summary>
    /// Batch update IsRead = true for all unread notifications.
    /// Uses EF Core ExecuteUpdateAsync — no entity loading.
    /// </summary>
    Task MarkAllReadAsync(int userId);

    Task DeleteNotifAsync(int notificationId, int userId);

    // ── Admin ─────────────────────────────────────────────────────────────
    /// <summary>Send a PLATFORM notification to a list of user IDs.</summary>
    Task SendBulkAsync(BroadcastRequest request);
}