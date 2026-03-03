using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core;

public interface IUserCacheService
{
    Task<IUser> GetUserAsync(string username);

    Task RemoveAsync(string username);

    Task SetAsync(IUser user);
}
