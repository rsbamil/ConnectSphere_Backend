using Microsoft.EntityFrameworkCore;
using ConnectSphere.Follow.Data;
using ConnectSphere.Follow.DTOs;
using ConnectSphere.Follow.Interfaces;

namespace ConnectSphere.Follow.Services;

/// <summary>
/// Social graph management.
/// After a successful follow/unfollow, calls Auth service to update
/// FollowerCount/FollowingCount on both users atomically.
/// Sends follow notifications via Notif service.
/// </summary>
public class FollowService : IFollowService
{
    private readonly FollowDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FollowService> _logger;

    public FollowService(FollowDbContext db, IHttpClientFactory httpFactory,
        ILogger<FollowService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<FollowDto> FollowUserAsync(int followerId, int followeeId)
    {
        if (followerId == followeeId)
            throw new InvalidOperationException("Users cannot follow themselves.");

        var existing = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);

        if (existing is not null)
            throw new InvalidOperationException("Already following or request pending.");

        // Check if followee has a private account via Auth service
        bool isPrivate = await IsPrivateAccountAsync(followeeId);

        var follow = new Models.Follow
        {
            FollowerId = followerId,
            FolloweeId = followeeId,
            Status = isPrivate ? "PENDING" : "ACCEPTED",
            CreatedAt = DateTime.UtcNow
        };

        _db.Follows.Add(follow);
        await _db.SaveChangesAsync();

        // If accepted immediately, update counters on both users
        if (!isPrivate)
        {
            _ = UpdateCounterAsync(followerId, "FollowingCount", 1);
            _ = UpdateCounterAsync(followeeId, "FollowerCount", 1);
        }

        // Send notification — FOLLOW_REQUEST for private, NEW_FOLLOWER for public
        _ = SendFollowNotificationAsync(followerId, followeeId, isPrivate);

        _logger.LogInformation("User {FollowerId} followed {FolloweeId} — Status={Status}",
            followerId, followeeId, follow.Status);

        return MapToDto(follow);
    }

    public async Task UnfollowUserAsync(int followerId, int followeeId)
    {
        var follow = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId)
            ?? throw new KeyNotFoundException("Follow relationship not found.");

        var wasAccepted = follow.Status == "ACCEPTED";
        _db.Follows.Remove(follow);
        await _db.SaveChangesAsync();

        // Only decrement counters if the follow was accepted
        if (wasAccepted)
        {
            _ = UpdateCounterAsync(followerId, "FollowingCount", -1);
            _ = UpdateCounterAsync(followeeId, "FollowerCount", -1);
        }

        _logger.LogInformation("User {FollowerId} unfollowed {FolloweeId}", followerId, followeeId);
    }

    public async Task<FollowDto> AcceptFollowRequestAsync(int followId, int followeeId)
    {
        var follow = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowId == followId && f.FolloweeId == followeeId && f.Status == "PENDING")
            ?? throw new KeyNotFoundException("Pending follow request not found.");

        follow.Status = "ACCEPTED";
        follow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Update counters on both users
        _ = UpdateCounterAsync(follow.FollowerId, "FollowingCount", 1);
        _ = UpdateCounterAsync(followeeId, "FollowerCount", 1);

        // Notify the follower that their request was accepted
        _ = SendFollowAcceptedNotificationAsync(followeeId, follow.FollowerId);

        _logger.LogInformation("Follow request {FollowId} accepted", followId);
        return MapToDto(follow);
    }

    public async Task RejectFollowRequestAsync(int followId, int followeeId)
    {
        var follow = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowId == followId && f.FolloweeId == followeeId && f.Status == "PENDING")
            ?? throw new KeyNotFoundException("Pending follow request not found.");

        _db.Follows.Remove(follow);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Follow request {FollowId} rejected", followId);
    }

    public async Task<IList<FollowDto>> GetFollowersAsync(int userId)
        => await _db.Follows
            .Where(f => f.FolloweeId == userId && f.Status == "ACCEPTED")
            .Select(f => MapToDto(f))
            .ToListAsync();

    public async Task<IList<FollowDto>> GetFollowingAsync(int userId)
        => await _db.Follows
            .Where(f => f.FollowerId == userId && f.Status == "ACCEPTED")
            .Select(f => MapToDto(f))
            .ToListAsync();

    public async Task<IList<FollowDto>> GetPendingRequestsAsync(int userId)
        => await _db.Follows
            .Where(f => f.FolloweeId == userId && f.Status == "PENDING")
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => MapToDto(f))
            .ToListAsync();

    public async Task<bool> IsFollowingAsync(int followerId, int followeeId)
        => await _db.Follows.AnyAsync(f =>
            f.FollowerId == followerId && f.FolloweeId == followeeId && f.Status == "ACCEPTED");

    public async Task<IList<int>> GetFollowingIdsAsync(int userId)
        => await _db.Follows
            .Where(f => f.FollowerId == userId && f.Status == "ACCEPTED")
            .Select(f => f.FolloweeId)
            .ToListAsync();

    public async Task<IList<int>> GetMutualFollowersAsync(int userAId, int userBId)
    {
        // Followers of A ∩ Followers of B
        var followersOfA = await _db.Follows
            .Where(f => f.FolloweeId == userAId && f.Status == "ACCEPTED")
            .Select(f => f.FollowerId).ToListAsync();

        var followersOfB = await _db.Follows
            .Where(f => f.FolloweeId == userBId && f.Status == "ACCEPTED")
            .Select(f => f.FollowerId).ToListAsync();

        return followersOfA.Intersect(followersOfB).ToList();
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task<bool> IsPrivateAccountAsync(int userId)
    {
        try
        {
            var client = _httpFactory.CreateClient("AuthService");
            var response = await client.GetFromJsonAsync<UserProfile>($"/api/users/{userId}");
            return response?.IsPrivate ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch privacy status for user {UserId}, defaulting to public", userId);
            return false;
        }
    }

    private async Task UpdateCounterAsync(int userId, string field, int delta)
    {
        try
        {
            var client = _httpFactory.CreateClient("AuthService");
            await client.PutAsJsonAsync($"/api/users/{userId}/counters", new { field, delta });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update {Field} for user {UserId}", field, userId);
        }
    }

    private async Task SendFollowNotificationAsync(int followerId, int followeeId, bool isPrivate)
    {
        try
        {
            var client = _httpFactory.CreateClient("NotifService");
            var payload = new
            {
                actorId = followerId,
                recipientId = followeeId,
                type = isPrivate ? "FOLLOW_REQUEST" : "NEW_FOLLOWER"
            };
            await client.PostAsJsonAsync("/api/notifications/follow", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send follow notification");
        }
    }

    private async Task SendFollowAcceptedNotificationAsync(int actorId, int recipientId)
    {
        try
        {
            var client = _httpFactory.CreateClient("NotifService");
            var payload = new { actorId, recipientId, type = "FOLLOW_ACCEPTED" };
            await client.PostAsJsonAsync("/api/notifications/follow", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send follow-accepted notification");
        }
    }

    private static FollowDto MapToDto(Models.Follow f) => new()
    {
        FollowId = f.FollowId,
        FollowerId = f.FollowerId,
        FolloweeId = f.FolloweeId,
        Status = f.Status,
        CreatedAt = f.CreatedAt
    };

    // Minimal DTO for Auth service response
    private record UserProfile(bool IsPrivate);
}