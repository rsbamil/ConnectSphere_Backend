using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Comment.Models;

/// <summary>
/// Supports two-level nesting via self-referential ParentCommentId.
/// ParentCommentId = null → top-level comment
/// ParentCommentId = someId → reply to that comment
///
/// Soft delete: IsDeleted = true replaces content with placeholder text in the DTO.
/// This preserves the thread structure so replies still make sense.
/// </summary>
[Table("comments")]
[Index(nameof(PostId), nameof(ParentCommentId))]  // GetTopLevelComments + GetReplies
[Index(nameof(UserId))]
public class Comment
{
    [Key]
    public int CommentId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    /// <summary>
    /// null = top-level comment; set = reply to that comment.
    /// Two levels max per spec.
    /// </summary>
    public int? ParentCommentId { get; set; }

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    // Denormalised — updated via ExecuteUpdateAsync
    public int LikeCount { get; set; } = 0;
    public int ReplyCount { get; set; } = 0;

    // Soft delete — content replaced with placeholder in DTO response
    public bool IsDeleted { get; set; } = false;

    // Set to true when EditComment() is called
    public bool IsEdited { get; set; } = false;
    public DateTime? EditedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}