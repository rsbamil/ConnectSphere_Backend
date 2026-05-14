using ConnectSphere.Post.DTOs;

namespace ConnectSphere.Post.Interfaces;

/// <summary>
/// Business logic layer for Post management.
/// </summary>
public interface IPostService
{
    Task<PostDto> CreatePostAsync(int userId, CreatePostRequest request);
    Task<PostDto?> GetPostByIdAsync(int postId, int? requestingUserId = null);
    Task<IList<PostDto>> GetPostsByUserAsync(int userId, int page = 1, int pageSize = 20);
    Task<IList<PostDto>> GetFeedAsync(int userId, IList<int> followingIds, int page = 1, int pageSize = 20);
    Task<IList<PostDto>> GetPublicPostsAsync(int page = 1, int pageSize = 20);
    Task<PostDto> UpdatePostAsync(int postId, int userId, UpdatePostRequest request);

    /// <summary>Soft delete — sets IsDeleted = true.</summary>
    Task DeletePostAsync(int postId, int userId, bool isAdmin = false);

    Task<IList<PostDto>> GetByHashtagAsync(string hashtag, int page = 1, int pageSize = 20);
    Task<IList<PostDto>> SearchPostsAsync(string query, int page = 1, int pageSize = 20);

    /// <summary>
    /// Ranks posts by composite score: LikeCount*3 + CommentCount*2 + ShareCount
    /// within the last 24 hours.
    /// </summary>
    Task<IList<PostDto>> GetTrendingPostsAsync(int topN = 20);

    /// <summary>
    /// Returns trending hashtags grouped from the last 48 hours.
    /// </summary>
    Task<IList<TrendingHashtagDto>> GetTrendingHashtagsAsync(int topN = 10);

    /// <summary>User timeline — all non-deleted posts by a specific user, newest first.</summary>
    Task<IList<PostDto>> GetUserTimelineAsync(int userId, int page = 1, int pageSize = 20);

    // ── Counter updates (called by Like/Comment services) ─────────────────
    Task IncrementLikeCountAsync(int postId, int delta = 1);
    Task IncrementCommentCountAsync(int postId, int delta = 1);
    Task IncrementShareCountAsync(int postId, int delta = 1);
}