using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Like.Models;

/// <summary>
/// Polymorphic like entity — covers both post likes and comment likes.
/// TargetType distinguishes POST from COMMENT, so no separate tables are needed.
/// Composite unique index prevents a user liking the same target twice.
/// </summary>
[Table("likes")]
[Index(nameof(UserId), nameof(TargetId), nameof(TargetType), IsUnique = true)]
[Index(nameof(TargetId), nameof(TargetType))]
public class Like
{
    [Key]
    public int LikeId { get; set; }

    public int UserId { get; set; }

    /// <summary>PostId or CommentId depending on TargetType.</summary>
    public int TargetId { get; set; }

    /// <summary>POST | COMMENT</summary>
    [Required, MaxLength(10)]
    public string TargetType { get; set; } = "POST";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}