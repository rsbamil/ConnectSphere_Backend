using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Follow.DTOs;
using ConnectSphere.Follow.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Follow.Controllers;

/// <summary>
/// REST endpoints for social graph management.
/// Route: /api/follows
/// </summary>
[ApiController]
[Route("api/follows")]
[Produces("application/json")]
public class FollowController : ControllerBase
{
    private readonly IFollowService _followService;

    public FollowController(IFollowService followService)
    {
        _followService = followService;
    }

    /// <summary>Follow a user. Public = accepted immediately; Private = pending.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Follow([FromBody] FollowRequest request)
    {
        var followerId = GetCurrentUserId();
        if (followerId == 0) return Unauthorized();
        try
        {
            var result = await _followService.FollowUserAsync(followerId, request.FolloweeId);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>Unfollow a user.</summary>
    [HttpDelete("{followeeId:int}")]
    [Authorize]
    public async Task<IActionResult> Unfollow(int followeeId)
    {
        var followerId = GetCurrentUserId();
        try
        {
            await _followService.UnfollowUserAsync(followerId, followeeId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Accept a pending follow request.</summary>
    [HttpPut("{followId:int}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(int followId)
    {
        var followeeId = GetCurrentUserId();
        try
        {
            var result = await _followService.AcceptFollowRequestAsync(followId, followeeId);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Reject a pending follow request.</summary>
    [HttpPut("{followId:int}/reject")]
    [Authorize]
    public async Task<IActionResult> Reject(int followId)
    {
        var followeeId = GetCurrentUserId();
        try
        {
            await _followService.RejectFollowRequestAsync(followId, followeeId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>List accepted followers of a user.</summary>
    [HttpGet("{userId:int}/followers")]
    [Authorize]
    public async Task<IActionResult> GetFollowers(int userId)
        => Ok(await _followService.GetFollowersAsync(userId));

    /// <summary>List users a user is following.</summary>
    [HttpGet("{userId:int}/following")]
    [Authorize]
    public async Task<IActionResult> GetFollowing(int userId)
        => Ok(await _followService.GetFollowingAsync(userId));

    /// <summary>List pending follow requests sent to the current user.</summary>
    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPending()
    {
        var userId = GetCurrentUserId();
        return Ok(await _followService.GetPendingRequestsAsync(userId));
    }

    /// <summary>Check whether current user follows a given user.</summary>
    [HttpGet("is-following/{followeeId:int}")]
    [Authorize]
    public async Task<IActionResult> IsFollowing(int followeeId)
    {
        var followerId = GetCurrentUserId();
        var result = await _followService.IsFollowingAsync(followerId, followeeId);
        return Ok(new { isFollowing = result });
    }

    /// <summary>Get accepted followee IDs — used internally by Feed service.</summary>
    [HttpGet("{userId:int}/following-ids")]
    [Authorize]
    public async Task<IActionResult> GetFollowingIds(int userId)
        => Ok(await _followService.GetFollowingIdsAsync(userId));

    /// <summary>Get mutual followers between two users.</summary>
    [HttpGet("mutual/{userAId:int}/{userBId:int}")]
    [Authorize]
    public async Task<IActionResult> GetMutual(int userAId, int userBId)
        => Ok(await _followService.GetMutualFollowersAsync(userAId, userBId));

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }
}