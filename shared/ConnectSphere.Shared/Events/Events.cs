namespace ConnectSphere.Shared.Events;

/// <summary>
/// Published by Post-Service when a new post is created.
/// Consumed by Feed-Service to fan-out the post to all followers' feeds.
/// </summary>
public class PostCreatedEvent
{
    public int PostId { get; set; }
    public int AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Published by Follow-Service when a follow action occurs.
/// </summary>
public class FollowCreatedEvent
{
    public int FollowerId { get; set; }
    public string Status { get; set; } = "ACCEPTED";
}