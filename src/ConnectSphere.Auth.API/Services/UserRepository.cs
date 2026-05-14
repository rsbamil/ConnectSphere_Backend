using Microsoft.EntityFrameworkCore;
using ConnectSphere.Auth.Data;
using ConnectSphere.Auth.Interfaces;
using ConnectSphere.Auth.Models;

namespace ConnectSphere.Auth.Services;

/// <summary>
/// EF Core implementation of IUserRepository.
/// Uses Neon (PostgreSQL) through AuthDbContext.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _db;

    public UserRepository(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<User?> FindByUserIdAsync(int userId)
        => await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);

    public async Task<User?> FindByUserNameAsync(string userName)
        => await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);

    public async Task<User?> FindByEmailAsync(string email)
        => await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> FindByGoogleIdAsync(string googleId)
        => await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);

    public async Task<bool> ExistsByUserNameAsync(string userName)
        => await _db.Users.AnyAsync(u => u.UserName == userName);

    public async Task<bool> ExistsByEmailAsync(string email)
        => await _db.Users.AnyAsync(u => u.Email == email);

    public async Task<IList<User>> SearchUsersAsync(string query, int page = 1, int pageSize = 20)
    {
        var lower = query.ToLower();
        return await _db.Users
            .Where(u => u.IsActive && (
                u.UserName.ToLower().Contains(lower) ||
                u.FullName.ToLower().Contains(lower)))
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IList<User>> FindAllActiveAsync(int page = 1, int pageSize = 20)
        => await _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<IList<User>> FindAllAsync(int page = 1, int pageSize = 20)
        => await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<User> CreateAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Atomically adjusts a counter field using EF Core ExecuteUpdateAsync.
    /// This avoids loading the full entity — essential for high-concurrency operations.
    /// </summary>
    public async Task UpdateCountersAsync(int userId, string field, int delta)
    {
        switch (field)
        {
            case "FollowerCount":
                await _db.Users
                    .Where(u => u.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        u => u.FollowerCount,
                        u => u.FollowerCount + delta));
                break;

            case "FollowingCount":
                await _db.Users
                    .Where(u => u.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        u => u.FollowingCount,
                        u => u.FollowingCount + delta));
                break;

            case "PostCount":
                await _db.Users
                    .Where(u => u.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        u => u.PostCount,
                        u => u.PostCount + delta));
                break;

            default:
                throw new ArgumentException($"Unknown counter field: {field}");
        }
    }

    public async Task<bool> DeleteAsync(int userId)
    {
        var rows = await _db.Users
            .Where(u => u.UserId == userId)
            .ExecuteDeleteAsync();
        return rows > 0;
    }
}