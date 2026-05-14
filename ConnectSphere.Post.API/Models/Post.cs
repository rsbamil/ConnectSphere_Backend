using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Post.Models;

/// <summary>
/// Core content entity. Visibility controls who can see this post.
/// Hashtags stored as comma-separated string (e.g. "#travel,#food") — no join table.
/// Counts are denormalised and updated atomically via ExecuteUpdateAsync.
/// </summary>
[Table("posts")]
[Index(nameof(UserId), nameof(CreatedAt))]       // Feed queries
[Index(nameof(Visibility), nameof(IsDeleted))]   // Public post queries
[Index(nameof(CreatedAt))]                        // Trending (last 24h)
public class Post
{
    [Key]
    public int PostId { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    // Azure Blob SAS URL — null for text-only posts
    public string? MediaUrl { get; set; }

    // IMAGE | VIDEO | GIF | null
    [MaxLength(10)]
    public string? MediaType { get; set; }

    // Denormalised — updated via ExecuteUpdateAsync
    public int LikeCount { get; set; } = 0;
    public int CommentCount { get; set; } = 0;
    public int ShareCount { get; set; } = 0;

    // Soft delete — preserves comment threads and like records for audit
    public bool IsDeleted { get; set; } = false;

    // PUBLIC | FOLLOWERS | PRIVATE
    [Required, MaxLength(15)]
    public string Visibility { get; set; } = "PUBLIC";

    // Comma-separated: "#travel,#food,#london"
    [MaxLength(500)]
    public string? Hashtags { get; set; }

    // If this is a repost/share of another post
    public int? OriginalPostId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Computed trending score — not stored in DB
    [NotMapped]
    public int TrendingScore => (LikeCount * 3) + (CommentCount * 2) + ShareCount;

    // Helper: split hashtags into a list
    public List<string> GetHashtagList()
        => string.IsNullOrWhiteSpace(Hashtags)
            ? new List<string>()
            : Hashtags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(h => h.Trim()).ToList();
}