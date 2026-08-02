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
    /// Evicts the current user from the persistence identity map so the next <see cref="GetCurrentUserAsync"/>
    /// re-reads it from the database. This lets a caller that serializes token refreshes observe a peer's
    /// committed refresh instead of the stale copy loaded earlier in the same request scope.
    /// </summary>
    /// <returns>The freshly loaded current user, or <see langword="null"/> when there is no authenticated user.</returns>
    Task<IUser> ReloadCurrentUserAsync();

    /// <summary>
    /// Persists changes made to the given user.
    /// </summary>
    /// <param name="user">The user to persist.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when the changes could not be persisted, so callers never report success after a failed save.</exception>
    Task UpdateUserAsync(IUser user);

    /// <summary>
    /// Durably commits pending user changes so a peer that reloads the user after this call observes them.
    /// This makes a token refresh visible to other requests before the refresh lock is released, rather than
    /// at the end of the ambient request scope.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when the pending changes could not be committed, so a caller never reports success after a failed commit.</exception>
    Task SaveChangesAsync();
}
