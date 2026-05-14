using ConnectSphere.Auth.Models;

namespace ConnectSphere.Auth.Interfaces;

/// <summary>
/// Data access layer for User entities.
/// All methods are async — returns Task&lt;T&gt; throughout.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByUserIdAsync(int userId);
    Task<User?> FindByUserNameAsync(string userName);
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByGoogleIdAsync(string googleId);
    Task<bool> ExistsByUserNameAsync(string userName);
    Task<bool> ExistsByEmailAsync(string email);

    /// <summary>EF Core LIKE search on UserName and FullName columns.</summary>
    Task<IList<User>> SearchUsersAsync(string query, int page = 1, int pageSize = 20);

    Task<IList<User>> FindAllActiveAsync(int page = 1, int pageSize = 20);
    Task<IList<User>> FindAllAsync(int page = 1, int pageSize = 20); // Admin only

    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);

    /// <summary>
    /// Atomically increments or decrements a counter field on the User record.
    /// Uses EF Core ExecuteUpdateAsync — no full entity load required.
    /// field: "FollowerCount" | "FollowingCount" | "PostCount"
    /// delta: +1 or -1
    /// </summary>
    Task UpdateCountersAsync(int userId, string field, int delta);

    Task<bool> DeleteAsync(int userId);
}