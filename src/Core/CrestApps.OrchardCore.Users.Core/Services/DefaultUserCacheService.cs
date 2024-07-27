using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Services;

public class DefaultUserCacheService : IUserCacheService
{
    private readonly Dictionary<string, User> _users = [];

    private readonly ILookupNormalizer _lookupNormalizer;
    private readonly IUserStore<IUser> _userStore;

    public DefaultUserCacheService(
        ILookupNormalizer lookupNormalizer,
        IUserStore<IUser> userStore)
    {
        _lookupNormalizer = lookupNormalizer;
        _userStore = userStore;
    }

    public async Task<IUser> GetUserAsync(string username)
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

    public Task SetAsync(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user is User u)
        {
            SetInternal(u);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string username)
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
