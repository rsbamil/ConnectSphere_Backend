using System.ComponentModel.DataAnnotations;

namespace ConnectSphere.Notif.DTOs;

// ── Inbound request DTOs (from other services) ────────────────────────────────

public class LikeNotifRequest
{
    public int ActorId { get; set; }
    public int TargetId { get; set; }
    public string TargetType { get; set; } = "POST"; // POST | COMMENT
    public string Type { get; set; } = "LIKE_POST";  // LIKE_POST | LIKE_COMMENT
}

public class CommentNotifRequest
{
    public int ActorId { get; set; }
    public int PostId { get; set; }
    public string Type { get; set; } = "NEW_COMMENT"; // NEW_COMMENT | NEW_REPLY
}

public class FollowNotifRequest
{
    public int ActorId { get; set; }
    public int RecipientId { get; set; }
    public string Type { get; set; } = "NEW_FOLLOWER"; // NEW_FOLLOWER | FOLLOW_REQUEST | FOLLOW_ACCEPTED
}

public class BroadcastRequest
{
    [Required]
    public IList<int> UserIds { get; set; } = new List<int>();

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Message { get; set; } = string.Empty;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class NotificationDto
{
    public int NotificationId { get; set; }
    public int RecipientId { get; set; }
    public int? ActorId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? TargetId { get; set; }
    public string? TargetType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}