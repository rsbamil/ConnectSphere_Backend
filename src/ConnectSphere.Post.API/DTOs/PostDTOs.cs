using System.ComponentModel.DataAnnotations;

namespace ConnectSphere.Post.DTOs;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreatePostRequest
{
    [Required, MinLength(1), MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public string? MediaUrl { get; set; }

    // IMAGE | VIDEO | GIF
    public string? MediaType { get; set; }

    // PUBLIC | FOLLOWERS | PRIVATE
    public string Visibility { get; set; } = "PUBLIC";

    // Comma-separated hashtags e.g. "#travel,#food"
    public string? Hashtags { get; set; }

    // Set when creating a repost/share
    public int? OriginalPostId { get; set; }
}

public class UpdatePostRequest
{
    [MaxLength(2000)]
    public string? Content { get; set; }

    public string? Visibility { get; set; }

    public string? Hashtags { get; set; }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class PostDto
{
    public int PostId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public string? Hashtags { get; set; }
    public List<string> HashtagList { get; set; } = new();
    public int? OriginalPostId { get; set; }
    public int TrendingScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TrendingHashtagDto
{
    public string Hashtag { get; set; } = string.Empty;
    public int Count { get; set; }
}