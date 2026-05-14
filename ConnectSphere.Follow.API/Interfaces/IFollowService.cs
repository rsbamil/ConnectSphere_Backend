using ConnectSphere.Follow.DTOs;

namespace ConnectSphere.Follow.Interfaces;

/// <summary>
/// Business logic for the social graph.
/// Counter updates on User entities are done via HTTP calls to Auth service.
/// </summary>
public interface IFollowService
{
    /// <summary>
    /// Follow a user. Public account → Status = ACCEPTED immediately.
    /// Private account → Status = PENDING, sends FOLLOW_REQUEST notification.
    /// </summary>
    Task<FollowDto> FollowUserAsync(int followerId, int followeeId);

    Task UnfollowUserAsync(int followerId, int followeeId);

    /// <summary>Accept a pending follow request. Updates counters on both users.</summary>
    Task<FollowDto> AcceptFollowRequestAsync(int followId, int followeeId);

    /// <summary>Reject a pending follow request. Deletes the Follow entity.</summary>
    Task RejectFollowRequestAsync(int followId, int followeeId);

    Task<IList<FollowDto>> GetFollowersAsync(int userId);
    Task<IList<FollowDto>> GetFollowingAsync(int userId);
    Task<IList<FollowDto>> GetPendingRequestsAsync(int userId);
    Task<bool> IsFollowingAsync(int followerId, int followeeId);

    /// <summary>
    /// Returns accepted followee IDs — used by Feed service to build home feed query.
    /// </summary>
    Task<IList<int>> GetFollowingIdsAsync(int userId);

    /// <summary>
    /// Returns intersection of follower sets for two users.
    /// Used for the "mutual followers" badge on profiles.
    /// </summary>
    Task<IList<int>> GetMutualFollowersAsync(int userAId, int userBId);
}