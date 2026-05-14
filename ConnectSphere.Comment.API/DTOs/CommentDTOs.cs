using System.ComponentModel.DataAnnotations;

namespace ConnectSphere.Comment.DTOs;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class AddCommentRequest
{
    public int PostId { get; set; }

    /// <summary>Null for top-level; set to parent comment's ID for a reply.</summary>
    public int? ParentCommentId { get; set; }

    [Required, MinLength(1), MaxLength(1000)]
    public string Content { get; set; } = string.Empty;
}

public class EditCommentRequest
{
    [Required, MinLength(1), MaxLength(1000)]
    public string Content { get; set; } = string.Empty;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class CommentDto
{
    public int CommentId { get; set; }
    public int PostId { get; set; }
    public int UserId { get; set; }
    public int? ParentCommentId { get; set; }

    /// <summary>
    /// Replaced with "This comment was deleted." when IsDeleted = true.
    /// Thread structure is preserved so replies remain visible.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}