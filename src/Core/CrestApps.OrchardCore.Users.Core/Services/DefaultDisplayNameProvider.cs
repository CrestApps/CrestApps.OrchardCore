using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Services;

public class DefaultDisplayNameProvider : IDisplayNameProvider
{
    public Task<string> GetAsync(IUser user)
         => Task.FromResult(user?.UserName ?? string.Empty);
}
