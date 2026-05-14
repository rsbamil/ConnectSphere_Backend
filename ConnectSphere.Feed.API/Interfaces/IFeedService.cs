namespace ConnectSphere.Feed.Interfaces;

/// <summary>
/// Feed business logic — home feed with Redis caching, explore feed,
/// trending hashtags, and user suggestions.
/// </summary>
public interface IFeedService
{
    /// <summary>
    /// Home feed — posts from followed users, newest first.
    /// Served from Redis IDistributedCache (5-min sliding TTL).
    /// Cache miss → query Post service → cache result.
    /// </summary>
    Task<IList<FeedPostDto>> GetFeedForUserAsync(int userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Explore feed — PUBLIC posts from non-followed users, ranked by engagement.
    /// </summary>
    Task<IList<FeedPostDto>> GetExploreFeedAsync(int userId, int page = 1, int pageSize = 20);

    /// <summary>User timeline — all public posts by a specific user, newest first.</summary>
    Task<IList<FeedPostDto>> GetUserTimelineAsync(int userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Called by MassTransit consumer on PostCreatedEvent.
    /// Inserts FeedItem for each follower and invalidates their Redis cache.
    /// </summary>
    Task AddPostToFollowerFeedsAsync(int postId, int authorId);

    /// <summary>Invalidate Redis cache entry for a user's feed.</summary>
    Task InvalidateFeedCacheAsync(int userId);

    /// <summary>Trending hashtags grouped from posts in the last 48 hours.</summary>
    Task<IList<TrendingHashtagDto>> GetTrendingHashtagsAsync(int topN = 10);

    /// <summary>Suggested users — mutual followers not yet followed, sorted by mutual count.</summary>
    Task<IList<SuggestedUserDto>> GetSuggestedUsersAsync(int userId);
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class FeedPostDto
{
    public int PostId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public string? Hashtags { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TrendingHashtagDto
{
    public string Hashtag { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class SuggestedUserDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int MutualFollowersCount { get; set; }
}