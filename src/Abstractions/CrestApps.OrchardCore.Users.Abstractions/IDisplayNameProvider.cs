using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Defines the contract for display name provider.
/// </summary>
public interface IDisplayNameProvider
{
    /// <summary>
    /// Gets the display name for the specified user.
    /// </summary>
    /// <param name="user">The user to describe.</param>
    Task<string> GetAsync(IUser user);
}
