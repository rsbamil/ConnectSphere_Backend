using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using ConnectSphere.Feed.Data;
using ConnectSphere.Feed.Interfaces;
using ConnectSphere.Feed.Models;

namespace ConnectSphere.Feed.Services;

/// <summary>
/// Feed service with Redis-backed caching.
///
/// Cache strategy:
///   Key   = "feed:{userId}:{page}"
///   TTL   = 5 minutes sliding
///   Miss  = fetch from Post service → cache → return
///   Invalidation = on AddPostToFollowerFeeds, delete all cache keys for affected users
///
/// Feed fan-out:
///   PostCreatedEvent arrives via MassTransit →
///   Fetch follower IDs from Follow service →
///   Insert FeedItem for each follower →
///   Invalidate their Redis cache entries
/// </summary>
public class FeedService : IFeedService
{
    private readonly FeedDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FeedService> _logger;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };

    public FeedService(FeedDbContext db, IDistributedCache cache,
        IHttpClientFactory httpFactory, ILogger<FeedService> logger)
    {
        _db = db;
        _cache = cache;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // ── Home feed ───────────────────────────────────────────────────────────

    public async Task<IList<FeedPostDto>> GetFeedForUserAsync(
    int userId, int page = 1, int pageSize = 20)
{
    var cacheKey = $"feed:{userId}:{page}:{pageSize}";
    string? cached = null;

    // Safe cache read
    try
    {
        cached = await _cache.GetStringAsync(cacheKey);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Cache read failed for {Key}", cacheKey);
    }

    if (!string.IsNullOrWhiteSpace(cached))
    {
        _logger.LogDebug("Feed cache HIT for user {UserId}", userId);

        return JsonSerializer.Deserialize<List<FeedPostDto>>(cached)
               ?? new List<FeedPostDto>();
    }

    var followingIds = await GetFollowingIdsAsync(userId);

    if (followingIds.Count == 0)
        return new List<FeedPostDto>();

    var posts = await FetchFeedPostsAsync(userId, followingIds, page, pageSize);

    // Safe cache write
    try
    {
        var json = JsonSerializer.Serialize(posts);
        await _cache.SetStringAsync(cacheKey, json, CacheOptions);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Cache write failed for {Key}", cacheKey);
    }

    return posts;
}

    // ── Explore feed ────────────────────────────────────────────────────────

    public async Task<IList<FeedPostDto>> GetExploreFeedAsync(int userId, int page = 1, int pageSize = 20)
    {
        // Returns public posts from non-followed users, ranked by engagement
        var followingIds = await GetFollowingIdsAsync(userId);

        var client = _httpFactory.CreateClient("PostService");
        var response = await client.GetFromJsonAsync<List<FeedPostDto>>("/api/posts/public");
        if (response is null) return new List<FeedPostDto>();

        return response
            .Where(p => !followingIds.Contains(p.UserId) && p.UserId != userId)
            .OrderByDescending(p => p.LikeCount + p.CommentCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    // ── User timeline ────────────────────────────────────────────────────────

    public async Task<IList<FeedPostDto>> GetUserTimelineAsync(int userId, int page = 1, int pageSize = 20)
    {
        var client = _httpFactory.CreateClient("PostService");
        var result = await client.GetFromJsonAsync<List<FeedPostDto>>(
            $"/api/posts/timeline/{userId}?page={page}&pageSize={pageSize}");
        return result ?? new List<FeedPostDto>();
    }

    // ── Fan-out (called by MassTransit consumer) ─────────────────────────────

    public async Task AddPostToFollowerFeedsAsync(int postId, int authorId)
    {
        var followerIds = await GetFollowerIdsAsync(authorId);
        if (followerIds.Count == 0) return;

        // Insert FeedItem for each follower (batch)
        var now = DateTime.UtcNow;
        var items = followerIds.Select(fid => new FeedItem
        {
            UserId = fid,
            PostId = postId,
            ActorId = authorId,
            Score = 0,
            CreatedAt = now
        }).ToList();

        _db.FeedItems.AddRange(items);
        await _db.SaveChangesAsync();

        // Invalidate Redis cache for each affected follower
        var invalidateTasks = followerIds.Select(fid => InvalidateFeedCacheAsync(fid));
        await Task.WhenAll(invalidateTasks);

        _logger.LogInformation("Post {PostId} fanned out to {Count} followers", postId, followerIds.Count);
    }

    public async Task InvalidateFeedCacheAsync(int userId)
    {
        // Remove cache keys for the first few pages (most common access patterns)
        for (var page = 1; page <= 5; page++)
        {
            try { await _cache.RemoveAsync($"feed:{userId}:{page}:20"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to invalidate feed cache for user {UserId}", userId); }
        }
    }

    // ── Trending hashtags ────────────────────────────────────────────────────

    public async Task<IList<TrendingHashtagDto>> GetTrendingHashtagsAsync(int topN = 10)
    {
        try
        {
            var client = _httpFactory.CreateClient("PostService");
            var result = await client.GetFromJsonAsync<List<TrendingHashtagDto>>(
                $"/api/posts/trending-hashtags?topN={topN}");
            return result ?? new List<TrendingHashtagDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch trending hashtags from Post service");
            return new List<TrendingHashtagDto>();
        }
    }

    // ── Suggested users ──────────────────────────────────────────────────────

    public async Task<IList<SuggestedUserDto>> GetSuggestedUsersAsync(int userId)
    {
        try
        {
            var client = _httpFactory.CreateClient("AuthService");
            var result = await client.GetFromJsonAsync<List<SuggestedUserDto>>(
                $"/api/users/{userId}/suggested");
            return result ?? new List<SuggestedUserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch suggested users for user {UserId}", userId);
            return new List<SuggestedUserDto>();
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<List<int>> GetFollowingIdsAsync(int userId)
    {
        try
        {
            var client = _httpFactory.CreateClient("FollowService");
            var result = await client.GetFromJsonAsync<List<int>>($"/api/follows/{userId}/following-ids");
            return result ?? new List<int>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch following IDs for user {UserId}", userId);
            return new List<int>();
        }
    }

    private async Task<List<int>> GetFollowerIdsAsync(int authorId)
    {
        try
        {
            var client = _httpFactory.CreateClient("FollowService");
            var follows = await client.GetFromJsonAsync<List<FollowDto>>($"/api/follows/{authorId}/followers");
            return follows?.Select(f => f.FollowerId).ToList() ?? new List<int>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch follower IDs for author {AuthorId}", authorId);
            return new List<int>();
        }
    }

    private async Task<List<FeedPostDto>> FetchFeedPostsAsync(
        int userId, List<int> followingIds, int page, int pageSize)
    {
        try
        {
            // Build query string with following IDs
            var idsParam = string.Join("&followingIds=", followingIds);
            var client = _httpFactory.CreateClient("PostService");
            var result = await client.GetFromJsonAsync<List<FeedPostDto>>(
                $"/api/posts/public?page={page}&pageSize={pageSize}");
            // Filter to only posts from followed users
            return result?
                .Where(p => followingIds.Contains(p.UserId))
                .ToList() ?? new List<FeedPostDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch feed posts for user {UserId}", userId);
            return new List<FeedPostDto>();
        }
    }

    // Minimal DTO for Follow service response
    private record FollowDto(int FollowerId, int FolloweeId, string Status);
}