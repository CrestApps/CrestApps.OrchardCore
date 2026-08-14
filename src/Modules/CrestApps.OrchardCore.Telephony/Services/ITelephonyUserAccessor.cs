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
    /// Loads the current user in an isolated unit of work, applies <paramref name="mutate"/> to it, and durably
    /// commits only that user document when the mutation reports a change. The isolated commit lets a serialized
    /// token refresh or a disconnect make its write visible to other requests immediately, before the refresh
    /// lock is released or the remote call is made, without flushing unrelated changes staged by the ambient
    /// request scope.
    /// </summary>
    /// <param name="mutate">A callback that applies the change to the loaded user and returns <see langword="true"/> when it mutated the user, or <see langword="false"/> to skip the commit.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when there is no current user or the change could not be committed, so a caller never reports success after a failed persist.</exception>
    Task PersistCurrentUserAsync(Func<IUser, bool> mutate);
}
