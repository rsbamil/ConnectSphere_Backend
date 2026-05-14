using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Like.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Like.Controllers;

/// <summary>
/// REST endpoints for likes.
/// Route: /api/likes
/// </summary>
[ApiController]
[Route("api/likes")]
[Produces("application/json")]
public class LikeController : ControllerBase
{
    private readonly ILikeService _likeService;

    public LikeController(ILikeService likeService)
    {
        _likeService = likeService;
    }

    /// <summary>
    /// Toggle like/unlike on a post or comment.
    /// Body: { "targetId": 1, "targetType": "POST" }
    /// Returns: { "liked": true }
    /// </summary>
    [HttpPost("toggle")]
    [Authorize]
    public async Task<IActionResult> Toggle([FromBody] ToggleLikeRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var liked = await _likeService.ToggleLikeAsync(userId, request.TargetId, request.TargetType);
        return Ok(new { liked });
    }

    /// <summary>Check whether the current user has liked a target.</summary>
    [HttpGet("has-liked")]
    [Authorize]
    public async Task<IActionResult> HasLiked(
        [FromQuery] int targetId, [FromQuery] string targetType)
    {
        var userId = GetCurrentUserId();
        var result = await _likeService.HasUserLikedAsync(userId, targetId, targetType);
        return Ok(new { hasLiked = result });
    }

    /// <summary>Get like count for a post or comment.</summary>
    [HttpGet("count")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCount(
        [FromQuery] int targetId, [FromQuery] string targetType)
    {
        var count = await _likeService.GetLikeCountAsync(targetId, targetType);
        return Ok(new { count });
    }

    /// <summary>Get list of userIds who liked a post — for the likers modal.</summary>
    [HttpGet("post/{postId:int}/likers")]
    [Authorize]
    public async Task<IActionResult> GetLikers(int postId)
        => Ok(await _likeService.GetLikersForPostAsync(postId));

    /// <summary>Get list of postIds that a user has liked.</summary>
    [HttpGet("user/{userId:int}/posts")]
    [Authorize]
    public async Task<IActionResult> GetLikedPosts(int userId)
        => Ok(await _likeService.GetLikedPostsByUserAsync(userId));

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }
}

public class ToggleLikeRequest
{
    public int TargetId { get; set; }
    public string TargetType { get; set; } = "POST"; // POST | COMMENT
}