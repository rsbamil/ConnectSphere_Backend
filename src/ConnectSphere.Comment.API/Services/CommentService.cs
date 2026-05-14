using Microsoft.EntityFrameworkCore;
using ConnectSphere.Comment.Data;
using ConnectSphere.Comment.DTOs;
using ConnectSphere.Comment.Interfaces;

namespace ConnectSphere.Comment.Services;

/// <summary>
/// Comment business logic.
/// On AddComment: increments Post.CommentCount via Post service HTTP call,
/// and dispatches a COMMENT notification via Notif service.
/// On DeleteComment: soft delete preserves thread structure.
/// </summary>
public class CommentService : ICommentService
{
    private readonly CommentDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CommentService> _logger;

    public CommentService(CommentDbContext db, IHttpClientFactory httpFactory,
        ILogger<CommentService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<CommentDto> AddCommentAsync(int userId, AddCommentRequest req)
    {
        var comment = new Models.Comment
        {
            PostId = req.PostId,
            UserId = userId,
            ParentCommentId = req.ParentCommentId,
            Content = req.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // If this is a reply, increment the parent's ReplyCount atomically
        if (req.ParentCommentId.HasValue)
        {
            await _db.Comments
                .Where(c => c.CommentId == req.ParentCommentId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReplyCount, c => c.ReplyCount + 1));
        }

        // Fire-and-forget: increment Post.CommentCount and send notification
        _ = IncrementPostCommentCountAsync(req.PostId);
        _ = SendCommentNotificationAsync(userId, req.PostId, req.ParentCommentId.HasValue);

        _logger.LogInformation("Comment {CommentId} added to post {PostId} by user {UserId}",
            comment.CommentId, req.PostId, userId);

        return MapToDto(comment);
    }

    public async Task<CommentDto?> GetCommentByIdAsync(int commentId)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId);
        return comment is null ? null : MapToDto(comment);
    }

    public async Task<IList<CommentDto>> GetCommentsByPostAsync(int postId, int page = 1, int pageSize = 50)
        => await _db.Comments
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync();

    public async Task<IList<CommentDto>> GetTopLevelCommentsAsync(int postId, int page = 1, int pageSize = 20)
        => await _db.Comments
            .Where(c => c.PostId == postId && c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync();

    public async Task<IList<CommentDto>> GetRepliesAsync(int commentId)
        => await _db.Comments
            .Where(c => c.ParentCommentId == commentId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => MapToDto(c))
            .ToListAsync();

    public async Task<IList<CommentDto>> GetCommentsByUserAsync(int userId, int page = 1, int pageSize = 20)
        => await _db.Comments
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync();

    public async Task<CommentDto> EditCommentAsync(int commentId, int userId, EditCommentRequest req)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.UserId != userId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        // Atomic update — no full entity load needed
        await _db.Comments
            .Where(c => c.CommentId == commentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Content, req.Content.Trim())
                .SetProperty(c => c.IsEdited, true)
                .SetProperty(c => c.EditedAt, DateTime.UtcNow));

        comment.Content = req.Content.Trim();
        comment.IsEdited = true;
        comment.EditedAt = DateTime.UtcNow;
        return MapToDto(comment);
    }

    public async Task DeleteCommentAsync(int commentId, int userId, bool isAdmin = false)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (!isAdmin && comment.UserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        // Soft delete — thread structure preserved; DTO returns placeholder
        await _db.Comments
            .Where(c => c.CommentId == commentId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));

        _logger.LogInformation("Comment {CommentId} soft-deleted", commentId);
    }

    public async Task<int> GetCommentCountAsync(int postId)
        => await _db.Comments.CountAsync(c => c.PostId == postId && !c.IsDeleted);

    public async Task IncrementLikeCountAsync(int commentId, int delta = 1)
        => await _db.Comments
            .Where(c => c.CommentId == commentId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LikeCount, c => c.LikeCount + delta));

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task IncrementPostCommentCountAsync(int postId)
    {
        try
        {
            var client = _httpFactory.CreateClient("PostService");
            await client.PutAsync($"/api/posts/{postId}/increment-comment?delta=1", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment comment count on post {PostId}", postId);
        }
    }

    private async Task SendCommentNotificationAsync(int actorId, int postId, bool isReply)
    {
        try
        {
            var client = _httpFactory.CreateClient("NotifService");
            var payload = new { actorId, postId, type = isReply ? "NEW_REPLY" : "NEW_COMMENT" };
            await client.PostAsJsonAsync("/api/notifications/comment", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send comment notification for post {PostId}", postId);
        }
    }

    /// <summary>
    /// Map Comment entity → CommentDto.
    /// Replaces content with placeholder when soft-deleted.
    /// </summary>
    private static CommentDto MapToDto(Models.Comment c) => new()
    {
        CommentId = c.CommentId,
        PostId = c.PostId,
        UserId = c.UserId,
        ParentCommentId = c.ParentCommentId,
        Content = c.IsDeleted ? "This comment was deleted." : c.Content,
        LikeCount = c.LikeCount,
        ReplyCount = c.ReplyCount,
        IsDeleted = c.IsDeleted,
        IsEdited = c.IsEdited,
        EditedAt = c.EditedAt,
        CreatedAt = c.CreatedAt
    };
}