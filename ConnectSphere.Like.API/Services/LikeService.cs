using Microsoft.EntityFrameworkCore;
using ConnectSphere.Like.Data;
using ConnectSphere.Like.Interfaces;
using ConnectSphere.Like.Models;

namespace ConnectSphere.Like.Services;

/// <summary>
/// Handles like/unlike logic with atomic EF Core transactions.
/// After a successful like, calls the Notification service and the Post/Comment
/// service counter endpoints via HTTP (IHttpClientFactory).
/// </summary>
public class LikeService : ILikeService
{
    private readonly LikeDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<LikeService> _logger;

    public LikeService(LikeDbContext db, IHttpClientFactory httpFactory,
        IConfiguration config, ILogger<LikeService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> ToggleLikeAsync(int userId, int targetId, string targetType)
{
    var normalType = targetType.ToUpper();

    try
    {
        var existing = await _db.Likes.FirstOrDefaultAsync(l =>
            l.UserId == userId &&
            l.TargetId == targetId &&
            l.TargetType == normalType);

        bool isLiked;

        if (existing == null)
        {
            _db.Likes.Add(new Models.Like
            {
                UserId = userId,
                TargetId = targetId,
                TargetType = normalType,
                CreatedAt = DateTime.UtcNow
            });

            isLiked = true;
        }
        else
        {
            _db.Likes.Remove(existing);
            isLiked = false;
        }

        await _db.SaveChangesAsync();

        _ = UpdateCounterAsync(targetId, normalType, isLiked ? 1 : -1);

        if (isLiked)
            _ = SendLikeNotificationAsync(userId, targetId, normalType);

        _logger.LogInformation(
            "User {UserId} {Action} {TargetType} {TargetId}",
            userId,
            isLiked ? "liked" : "unliked",
            normalType,
            targetId);

        return isLiked;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "ToggleLike failed");
        throw;
    }
}

    public async Task<bool> HasUserLikedAsync(int userId, int targetId, string targetType)
        => await _db.Likes.AnyAsync(l =>
            l.UserId == userId && l.TargetId == targetId && l.TargetType == targetType.ToUpper());

    public async Task<int> GetLikeCountAsync(int targetId, string targetType)
        => await _db.Likes.CountAsync(l =>
            l.TargetId == targetId && l.TargetType == targetType.ToUpper());

    public async Task<IList<int>> GetLikersForPostAsync(int postId)
        => await _db.Likes
            .Where(l => l.TargetId == postId && l.TargetType == "POST")
            .Select(l => l.UserId)
            .ToListAsync();

    public async Task<IList<int>> GetLikedPostsByUserAsync(int userId)
        => await _db.Likes
            .Where(l => l.UserId == userId && l.TargetType == "POST")
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => l.TargetId)
            .ToListAsync();

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task UpdateCounterAsync(int targetId, string targetType, int delta)
    {
        try
        {
            var client = _httpFactory.CreateClient("PostService");
            if (targetType == "POST")
                await client.PutAsync($"/api/posts/{targetId}/increment-like?delta={delta}", null);
            // For COMMENT type, would call Comment service
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update like counter for {TargetType} {TargetId}", targetType, targetId);
        }
    }

    private async Task SendLikeNotificationAsync(int actorId, int targetId, string targetType)
    {
        try
        {
            var client = _httpFactory.CreateClient("NotifService");
            var payload = new
            {
                actorId,
                targetId,
                targetType,
                type = targetType == "POST" ? "LIKE_POST" : "LIKE_COMMENT"
            };
            await client.PostAsJsonAsync("/api/notifications/like", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send like notification for {TargetType} {TargetId}", targetType, targetId);
        }
    }
}