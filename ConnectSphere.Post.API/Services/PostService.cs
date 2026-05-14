using MassTransit;
using Microsoft.EntityFrameworkCore;
using ConnectSphere.Post.Data;
using ConnectSphere.Post.DTOs;
using ConnectSphere.Post.Interfaces;
using ConnectSphere.Shared.Events;

namespace ConnectSphere.Post.Services;

/// <summary>
/// Post business logic. On CreatePost, publishes PostCreatedEvent to RabbitMQ
/// so the Feed-Service can fan out the post to all followers' feeds asynchronously.
/// </summary>
public class PostService : IPostService
{
    private readonly PostDbContext _db;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<PostService> _logger;

    public PostService(PostDbContext db, IPublishEndpoint bus, ILogger<PostService> logger)
    {
        _db = db;
        _bus = bus;
        _logger = logger;
    }

    public async Task<PostDto> CreatePostAsync(int userId, CreatePostRequest req)
    {
        var post = new Models.Post
        {
            UserId = userId,
            Content = req.Content.Trim(),
            MediaUrl = req.MediaUrl,
            MediaType = req.MediaType?.ToUpper(),
            Visibility = req.Visibility?.ToUpper() ?? "PUBLIC",
            Hashtags = NormaliseHashtags(req.Hashtags),
            OriginalPostId = req.OriginalPostId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        // Publish async event — Feed-Service consumer will fan-out to followers
        await _bus.Publish(new PostCreatedEvent
        {
            PostId = post.PostId,
            AuthorId = userId,
            CreatedAt = post.CreatedAt
        });

        _logger.LogInformation("Post {PostId} created by user {UserId}", post.PostId, userId);
        return MapToDto(post);
    }

    public async Task<PostDto?> GetPostByIdAsync(int postId, int? requestingUserId = null)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted);
        if (post is null) return null;

        // Enforce visibility
        if (post.Visibility == "PRIVATE" && post.UserId != requestingUserId) return null;

        return MapToDto(post);
    }

    public async Task<IList<PostDto>> GetPostsByUserAsync(int userId, int page = 1, int pageSize = 20)
        => await _db.Posts
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

    public async Task<IList<PostDto>> GetFeedAsync(int userId, IList<int> followingIds, int page = 1, int pageSize = 20)
        => await _db.Posts
            .Where(p => followingIds.Contains(p.UserId) && !p.IsDeleted &&
                        (p.Visibility == "PUBLIC" || p.Visibility == "FOLLOWERS"))
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

    public async Task<IList<PostDto>> GetPublicPostsAsync(int page = 1, int pageSize = 20)
        => await _db.Posts
            .Where(p => p.Visibility == "PUBLIC" && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

    public async Task<PostDto> UpdatePostAsync(int postId, int userId, UpdatePostRequest req)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        if (post.UserId != userId)
            throw new UnauthorizedAccessException("You can only edit your own posts.");

        if (req.Content is not null) post.Content = req.Content.Trim();
        if (req.Visibility is not null) post.Visibility = req.Visibility.ToUpper();
        if (req.Hashtags is not null) post.Hashtags = NormaliseHashtags(req.Hashtags);
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(post);
    }

    public async Task DeletePostAsync(int postId, int userId, bool isAdmin = false)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        if (!isAdmin && post.UserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own posts.");

        // Soft delete — preserves comment and like records
        await _db.Posts
            .Where(p => p.PostId == postId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

        _logger.LogInformation("Post {PostId} soft-deleted by {Actor}", postId, isAdmin ? "admin" : $"user {userId}");
    }

    public async Task<IList<PostDto>> GetByHashtagAsync(string hashtag, int page = 1, int pageSize = 20)
    {
        // Ensure hashtag has # prefix for matching
        var tag = hashtag.StartsWith('#') ? hashtag : $"#{hashtag}";
        return await _db.Posts
            .Where(p => !p.IsDeleted && p.Visibility == "PUBLIC" && p.Hashtags != null && p.Hashtags.Contains(tag))
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<IList<PostDto>> SearchPostsAsync(string query, int page = 1, int pageSize = 20)
    {
        var lower = query.ToLower();
        return await _db.Posts
            .Where(p => !p.IsDeleted && p.Visibility == "PUBLIC" && p.Content.ToLower().Contains(lower))
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<IList<PostDto>> GetTrendingPostsAsync(int topN = 20)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        return await _db.Posts
            .Where(p => !p.IsDeleted && p.Visibility == "PUBLIC" && p.CreatedAt >= cutoff)
            .OrderByDescending(p => (p.LikeCount * 3) + (p.CommentCount * 2) + p.ShareCount)
            .Take(topN)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<IList<TrendingHashtagDto>> GetTrendingHashtagsAsync(int topN = 10)
    {
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var posts = await _db.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= cutoff && p.Hashtags != null)
            .Select(p => p.Hashtags!)
            .ToListAsync();

        // Split hashtags and count frequency in memory
        return posts
            .SelectMany(h => h.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(h => h.Trim().ToLower())
            .Where(h => h.StartsWith('#'))
            .GroupBy(h => h)
            .Select(g => new TrendingHashtagDto { Hashtag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToList();
    }

    public async Task<IList<PostDto>> GetUserTimelineAsync(int userId, int page = 1, int pageSize = 20)
        => await _db.Posts
            .Where(p => p.UserId == userId && !p.IsDeleted && p.Visibility == "PUBLIC")
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

    // ── Atomic counter updates ─────────────────────────────────────────────

    public async Task IncrementLikeCountAsync(int postId, int delta = 1)
        => await _db.Posts
            .Where(p => p.PostId == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LikeCount, p => p.LikeCount + delta));

    public async Task IncrementCommentCountAsync(int postId, int delta = 1)
        => await _db.Posts
            .Where(p => p.PostId == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CommentCount, p => p.CommentCount + delta));

    public async Task IncrementShareCountAsync(int postId, int delta = 1)
        => await _db.Posts
            .Where(p => p.PostId == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ShareCount, p => p.ShareCount + delta));

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string? NormaliseHashtags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var tags = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLower())
            .Select(t => t.StartsWith('#') ? t : $"#{t}")
            .Distinct();
        return string.Join(',', tags);
    }

    private static PostDto MapToDto(Models.Post p) => new()
    {
        PostId = p.PostId,
        UserId = p.UserId,
        Content = p.Content,
        MediaUrl = p.MediaUrl,
        MediaType = p.MediaType,
        LikeCount = p.LikeCount,
        CommentCount = p.CommentCount,
        ShareCount = p.ShareCount,
        Visibility = p.Visibility,
        Hashtags = p.Hashtags,
        HashtagList = p.GetHashtagList(),
        OriginalPostId = p.OriginalPostId,
        TrendingScore = p.TrendingScore,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}