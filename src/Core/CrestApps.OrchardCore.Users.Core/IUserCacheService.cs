using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core;

public interface IUserCacheService
{
    /// <summary>
    /// Gets the cached user for the specified username.
    /// </summary>
    /// <param name="username">The username to look up.</param>
    Task<IUser> GetUserAsync(string username);

    /// <summary>
    /// Removes the cached user for the specified username.
    /// </summary>
    /// <param name="username">The username to evict from the cache.</param>
    Task RemoveAsync(string username);

    /// <summary>
    /// Stores the specified user in the cache.
    /// </summary>
    /// <param name="user">The user to cache.</param>
    Task SetAsync(IUser user);
}
