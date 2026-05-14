using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Feed.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Feed.Controllers;

/// <summary>
/// REST endpoints for the feed and discovery features.
/// Route: /api/feed
/// </summary>
[ApiController]
[Route("api/feed")]
[Produces("application/json")]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    /// <summary>
    /// Home feed — posts from followed users, newest first.
    /// Served from Redis (5-min TTL). Cache miss rebuilds from Post service.
    /// </summary>
    [HttpGet("{userId:int}")]
    [Authorize]
    public async Task<IActionResult> GetFeed(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        return Ok(await _feedService.GetFeedForUserAsync(userId, page, pageSize));
    }

    /// <summary>
    /// Explore feed — public posts from non-followed users, ranked by engagement.
    /// </summary>
    [HttpGet("explore/{userId:int}")]
    [Authorize]
    public async Task<IActionResult> GetExplore(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        return Ok(await _feedService.GetExploreFeedAsync(userId, page, pageSize));
    }

    /// <summary>Public timeline for any user — no auth required.</summary>
    [HttpGet("timeline/{userId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTimeline(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _feedService.GetUserTimelineAsync(userId, page, pageSize));

    /// <summary>Trending hashtags from the last 48 hours.</summary>
    [HttpGet("trending-hashtags")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrendingHashtags([FromQuery] int topN = 10)
        => Ok(await _feedService.GetTrendingHashtagsAsync(topN));

    /// <summary>Suggested users to follow — mutual followers not yet followed.</summary>
    [HttpGet("suggestions/{userId:int}")]
    [Authorize]
    public async Task<IActionResult> GetSuggestions(int userId)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        return Ok(await _feedService.GetSuggestedUsersAsync(userId));
    }

    /// <summary>Manually invalidate feed cache (e.g. after unfollow).</summary>
    [HttpDelete("cache/{userId:int}")]
    [Authorize]
    public async Task<IActionResult> InvalidateCache(int userId)
    {
        if (!IsOwnerOrAdmin(userId)) return Forbid();
        await _feedService.InvalidateFeedCacheAsync(userId);
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    private bool IsOwnerOrAdmin(int userId)
        => User.IsInRole("Admin") || GetCurrentUserId() == userId;
}