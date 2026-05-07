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
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetAsync(IUser user, CancellationToken cancellationToken = default);
}
