using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Notif.Models;

/// <summary>
/// Represents a single in-app notification.
///
/// Notification types:
///   LIKE_POST        — someone liked your post
///   LIKE_COMMENT     — someone liked your comment
///   NEW_COMMENT      — someone commented on your post
///   NEW_REPLY        — someone replied to your comment
///   NEW_FOLLOWER     — someone followed you (public account)
///   FOLLOW_REQUEST   — someone sent a follow request (private account)
///   FOLLOW_ACCEPTED  — your follow request was accepted
///   MENTION          — you were @mentioned in a post or comment
///   PLATFORM         — broadcast message from admin
/// </summary>
[Table("notifications")]
[Index(nameof(RecipientId), nameof(IsRead))]  // Fast unread count queries
[Index(nameof(RecipientId), nameof(CreatedAt))]
public class Notification
{
    [Key]
    public int NotificationId { get; set; }

    /// <summary>User receiving the notification.</summary>
    public int RecipientId { get; set; }

    /// <summary>User who triggered the notification (null for PLATFORM type).</summary>
    public int? ActorId { get; set; }

    [Required, MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Message { get; set; }

    /// <summary>Post ID, Comment ID, or User ID depending on Type.</summary>
    public int? TargetId { get; set; }

    /// <summary>POST | COMMENT | USER</summary>
    [MaxLength(10)]
    public string? TargetType { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}