using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Post.DTOs;
using ConnectSphere.Post.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Post.Controllers;

/// <summary>
/// REST endpoints for Post management.
/// Route: /api/posts
/// GET public/hashtag/trending — no [Authorize] required.
/// POST/PUT/DELETE — require [Authorize].
/// </summary>
[ApiController]
[Route("api/posts")]
[Produces("application/json")]
public class PostController : ControllerBase
{
    private readonly IPostService _postService;

    public PostController(IPostService postService)
    {
        _postService = postService;
    }

    // ── Public read endpoints (no auth required) ────────────────────────────

    /// <summary>Get all public posts with pagination.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _postService.GetPublicPostsAsync(page, pageSize));

    /// <summary>Get posts by hashtag (e.g. /api/posts/hashtag/travel).</summary>
    [HttpGet("hashtag/{tag}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByHashtag(string tag,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _postService.GetByHashtagAsync(tag, page, pageSize));

    /// <summary>Get trending posts ranked by composite score (last 24h).</summary>
    [HttpGet("trending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrending([FromQuery] int topN = 20)
        => Ok(await _postService.GetTrendingPostsAsync(topN));

    /// <summary>Get trending hashtags from the last 48 hours.</summary>
    [HttpGet("trending-hashtags")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrendingHashtags([FromQuery] int topN = 10)
        => Ok(await _postService.GetTrendingHashtagsAsync(topN));

    /// <summary>Search posts by keyword (authenticated users only).</summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search([FromQuery] string q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _postService.SearchPostsAsync(q, page, pageSize));

    /// <summary>Get a single post by ID.</summary>
    [HttpGet("{postId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int postId)
    {
        var userId = GetCurrentUserId();
        var post = await _postService.GetPostByIdAsync(postId, userId);
        return post is null ? NotFound() : Ok(post);
    }

    /// <summary>Get all posts by a specific user.</summary>
    [HttpGet("by-user/{userId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByUser(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _postService.GetPostsByUserAsync(userId, page, pageSize));

    /// <summary>Get user's public timeline.</summary>
    [HttpGet("timeline/{userId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTimeline(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _postService.GetUserTimelineAsync(userId, page, pageSize));

    // ── Authenticated write endpoints ───────────────────────────────────────

    /// <summary>Create a new post. Publishes PostCreatedEvent for async feed fan-out.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var post = await _postService.CreatePostAsync(userId, request);
        return CreatedAtAction(nameof(GetById), new { postId = post.PostId }, post);
    }

    /// <summary>Update own post content, visibility, or hashtags.</summary>
    [HttpPut("{postId:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int postId, [FromBody] UpdatePostRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var post = await _postService.UpdatePostAsync(postId, userId, request);
            return Ok(post);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Soft delete a post (sets IsDeleted = true).</summary>
    [HttpDelete("{postId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int postId)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        try
        {
            await _postService.DeletePostAsync(postId, userId, isAdmin);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ── Internal counter endpoints (called by Like/Comment services) ────────

    [HttpPut("{postId:int}/increment-like")]
    [Authorize]
    public async Task<IActionResult> IncrementLike(int postId, [FromQuery] int delta = 1)
    {
        await _postService.IncrementLikeCountAsync(postId, delta);
        return NoContent();
    }

    [HttpPut("{postId:int}/increment-comment")]
    [Authorize]
    public async Task<IActionResult> IncrementComment(int postId, [FromQuery] int delta = 1)
    {
        await _postService.IncrementCommentCountAsync(postId, delta);
        return NoContent();
    }

    [HttpPut("{postId:int}/increment-share")]
    [Authorize]
    public async Task<IActionResult> IncrementShare(int postId, [FromQuery] int delta = 1)
    {
        await _postService.IncrementShareCountAsync(postId, delta);
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }
}