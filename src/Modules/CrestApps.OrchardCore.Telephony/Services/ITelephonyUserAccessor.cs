using OrchardCore.Users;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Provides access to the current authenticated user and persists changes to it. This seam isolates
/// the user management dependency so the token store can be unit tested.
/// </summary>
public interface ITelephonyUserAccessor
{
    /// <summary>
    /// Gets the current authenticated user, or <see langword="null"/> when there is no authenticated user.
    /// </summary>
    /// <returns>The current user, or <see langword="null"/>.</returns>
    Task<IUser> GetCurrentUserAsync();

    /// <summary>
    /// Persists changes made to the given user.
    /// </summary>
    /// <param name="user">The user to persist.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateUserAsync(IUser user);
}
