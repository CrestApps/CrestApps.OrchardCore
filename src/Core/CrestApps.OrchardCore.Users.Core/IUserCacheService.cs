using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core;

/// <summary>
/// Defines the contract for user cache service.
/// </summary>
public interface IUserCacheService
{
    /// <summary>
    /// Gets the cached user for the specified username.
    /// </summary>
    /// <param name="username">The username to look up.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IUser> GetUserAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the cached user for the specified username.
    /// </summary>
    /// <param name="username">The username to evict from the cache.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RemoveAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the specified user in the cache.
    /// </summary>
    /// <param name="user">The user to cache.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SetAsync(IUser user, CancellationToken cancellationToken = default);
}
