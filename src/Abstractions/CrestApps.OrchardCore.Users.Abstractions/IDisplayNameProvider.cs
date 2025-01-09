using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users;

public interface IDisplayNameProvider
{
    Task<string> GetAsync(IUser user);
}
