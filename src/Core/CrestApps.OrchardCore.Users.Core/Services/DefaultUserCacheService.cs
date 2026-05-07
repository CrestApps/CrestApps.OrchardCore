using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// A scoped in-memory implementation of <see cref="IUserCacheService"/> that caches user objects by username.
/// </summary>
public sealed class DefaultUserCacheService : IUserCacheService
{
    private readonly Dictionary<string, User> _users = [];

    private readonly ILookupNormalizer _lookupNormalizer;
    private readonly IUserStore<IUser> _userStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserCacheService"/> class.
    /// </summary>
    /// <param name="lookupNormalizer">The normalizer used to normalize usernames for store lookups.</param>
    /// <param name="userStore">The user store used to retrieve users when not found in cache.</param>
    public DefaultUserCacheService(
        ILookupNormalizer lookupNormalizer,
        IUserStore<IUser> userStore)
    {
        _lookupNormalizer = lookupNormalizer;
        _userStore = userStore;
    }

    /// <inheritdoc />
    public async Task<IUser> GetUserAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        if (!_users.TryGetValue(username, out var user))
        {
            var normalizedUsername = _lookupNormalizer.NormalizeName(username);
            var appUser = await _userStore.FindByNameAsync(normalizedUsername, default);

            if (appUser is User u)
            {
                user = u;

                SetInternal(u);
            }
        }

        return user;
    }

    /// <inheritdoc />
    public Task SetAsync(IUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user is User u)
        {
            SetInternal(u);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        _users.Remove(username);

        return Task.CompletedTask;
    }

    private void SetInternal(User user)
    {
        _users[user.UserName] = user;
    }
}
