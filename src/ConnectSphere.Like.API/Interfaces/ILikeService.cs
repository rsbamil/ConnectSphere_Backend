namespace ConnectSphere.Like.Interfaces;

public interface ILikeService
{
    /// <summary>
    /// Toggle like/unlike. Returns true if liked, false if unliked.
    /// Wraps add+increment or remove+decrement in an EF Core transaction.
    /// Also calls the Notif service on like (not on unlike).
    /// </summary>
    Task<bool> ToggleLikeAsync(int userId, int targetId, string targetType);

    Task<bool> HasUserLikedAsync(int userId, int targetId, string targetType);
    Task<int> GetLikeCountAsync(int targetId, string targetType);

    /// <summary>Returns list of userIds who liked a post — for the likers modal.</summary>
    Task<IList<int>> GetLikersForPostAsync(int postId);

    /// <summary>Returns list of postIds that a user has liked.</summary>
    Task<IList<int>> GetLikedPostsByUserAsync(int userId);
}