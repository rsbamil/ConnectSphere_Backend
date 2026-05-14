using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConnectSphere.Comment.DTOs;
using ConnectSphere.Comment.Interfaces;
using System.Security.Claims;

namespace ConnectSphere.Comment.Controllers;

/// <summary>
/// REST endpoints for comments and replies.
/// Route: /api/comments
/// </summary>
[ApiController]
[Route("api/comments")]
[Produces("application/json")]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    /// <summary>Add a comment or reply to a post.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add([FromBody] AddCommentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var comment = await _commentService.AddCommentAsync(userId, request);
        return CreatedAtAction(nameof(GetById), new { commentId = comment.CommentId }, comment);
    }

    /// <summary>Get a single comment by ID.</summary>
    [HttpGet("{commentId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int commentId)
    {
        var comment = await _commentService.GetCommentByIdAsync(commentId);
        return comment is null ? NotFound() : Ok(comment);
    }

    /// <summary>Get all top-level comments for a post.</summary>
    [HttpGet("post/{postId:int}/top-level")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTopLevel(int postId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _commentService.GetTopLevelCommentsAsync(postId, page, pageSize));

    /// <summary>Get replies to a specific comment.</summary>
    [HttpGet("{commentId:int}/replies")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReplies(int commentId)
        => Ok(await _commentService.GetRepliesAsync(commentId));

    /// <summary>Get all comments by a user.</summary>
    [HttpGet("by-user/{userId:int}")]
    [Authorize]
    public async Task<IActionResult> GetByUser(int userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _commentService.GetCommentsByUserAsync(userId, page, pageSize));

    /// <summary>Get comment count for a post.</summary>
    [HttpGet("post/{postId:int}/count")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCount(int postId)
    {
        var count = await _commentService.GetCommentCountAsync(postId);
        return Ok(new { count });
    }

    /// <summary>Edit own comment. Sets IsEdited = true.</summary>
    [HttpPut("{commentId:int}")]
    [Authorize]
    public async Task<IActionResult> Edit(int commentId, [FromBody] EditCommentRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var updated = await _commentService.EditCommentAsync(commentId, userId, request);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>
    /// Soft delete a comment. Content is replaced with placeholder text.
    /// Admin can delete any comment.
    /// </summary>
    [HttpDelete("{commentId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int commentId)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        try
        {
            await _commentService.DeleteCommentAsync(commentId, userId, isAdmin);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Internal: increment like count on a comment (called by Like service).</summary>
    [HttpPut("{commentId:int}/increment-like")]
    [Authorize]
    public async Task<IActionResult> IncrementLike(int commentId, [FromQuery] int delta = 1)
    {
        await _commentService.IncrementLikeCountAsync(commentId, delta);
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }
}