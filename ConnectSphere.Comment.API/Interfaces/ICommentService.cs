using ConnectSphere.Comment.DTOs;

namespace ConnectSphere.Comment.Interfaces;

/// <summary>
/// Business logic for Comment management.
/// AddComment calls Post service to increment CommentCount and Notif service to send alert.
/// </summary>
public interface ICommentService
{
    Task<CommentDto> AddCommentAsync(int userId, AddCommentRequest request);
    Task<CommentDto?> GetCommentByIdAsync(int commentId);

    /// <summary>All comments for a post (flat list including replies).</summary>
    Task<IList<CommentDto>> GetCommentsByPostAsync(int postId, int page = 1, int pageSize = 50);

    /// <summary>Only top-level comments (ParentCommentId IS NULL).</summary>
    Task<IList<CommentDto>> GetTopLevelCommentsAsync(int postId, int page = 1, int pageSize = 20);

    /// <summary>Replies to a specific comment (ParentCommentId = commentId).</summary>
    Task<IList<CommentDto>> GetRepliesAsync(int commentId);

    Task<IList<CommentDto>> GetCommentsByUserAsync(int userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Sets IsEdited = true, EditedAt = UtcNow, updates Content.
    /// Uses ExecuteUpdateAsync — no full entity load.
    /// </summary>
    Task<CommentDto> EditCommentAsync(int commentId, int userId, EditCommentRequest request);

    /// <summary>
    /// Soft delete — sets IsDeleted = true.
    /// DTO will return "This comment was deleted." as content.
    /// </summary>
    Task DeleteCommentAsync(int commentId, int userId, bool isAdmin = false);

    Task<int> GetCommentCountAsync(int postId);

    /// <summary>Increment like count (called by Like service).</summary>
    Task IncrementLikeCountAsync(int commentId, int delta = 1);
}