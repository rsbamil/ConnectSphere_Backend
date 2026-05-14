using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConnectSphere.Follow.Models;

/// <summary>
/// Represents a directional follow relationship: Follower → Followee.
///
/// Status lifecycle:
///   Public accounts:  PENDING → (immediate) ACCEPTED
///   Private accounts: PENDING → ACCEPTED (via AcceptFollowRequest)
///                     PENDING → REJECTED (via RejectFollowRequest, entity deleted)
///
/// Composite unique index on (FollowerId, FolloweeId) prevents duplicate follows.
/// </summary>
[Table("follows")]
[Index(nameof(FollowerId), nameof(FolloweeId), IsUnique = true)]
[Index(nameof(FolloweeId))]   // For "list of followers" queries
[Index(nameof(FollowerId))]   // For "list of following" queries
public class Follow
{
    [Key]
    public int FollowId { get; set; }

    public int FollowerId { get; set; }

    public int FolloweeId { get; set; }

    /// <summary>PENDING | ACCEPTED</summary>
    [Required, MaxLength(10)]
    public string Status { get; set; } = "PENDING";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}