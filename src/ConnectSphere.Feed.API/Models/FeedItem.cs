using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Feed.Models;

/// <summary>
/// A row in a user's home feed.
/// Written by the MassTransit consumer when a followed author creates a post.
/// Redis cache is the primary read path (5-min sliding TTL).
/// This table is the fallback / source of truth when cache is cold.
/// </summary>
[Table("feed_items")]
[Index(nameof(UserId), nameof(CreatedAt))]  // Paginated feed queries
public class FeedItem
{
    [Key]
    public int FeedItemId { get; set; }

    /// <summary>The user whose home feed contains this item.</summary>
    public int UserId { get; set; }

    /// <summary>The post being surfaced in this feed slot.</summary>
    public int PostId { get; set; }

    /// <summary>The post's author.</summary>
    public int ActorId { get; set; }

    /// <summary>
    /// Engagement score at insert time: LikeCount + CommentCount.
    /// Used by Explore feed ranking.
    /// </summary>
    public decimal Score { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional expiry tracking — mirrors Redis TTL logic.</summary>
    public DateTime? ExpiresAt { get; set; }
}