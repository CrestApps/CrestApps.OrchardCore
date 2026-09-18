namespace CrestApps.Core.Security;

/// <summary>
/// Reads and durably writes the suite's own data on the signed-in user's profile.
/// </summary>
/// <remarks>
/// Separate from <see cref="IUserDirectory"/> because the requirements are different. This writes,
/// it writes only for the current user, and the write has to be durable on its own: a soft-phone
/// credential refresh must be visible to other requests before the refresh lock is released, without
/// flushing whatever else the ambient request happened to have staged.
/// </remarks>
public interface IUserProfileStore
{
    /// <summary>
    /// Reads the suite's data of the given shape from the current user's profile.
    /// </summary>
    /// <typeparam name="T">The data shape.</typeparam>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored data, or <see langword="null"/> when the user has none or there is no current user.</returns>
    Task<T> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Applies a change to the suite's data on the current user's profile and commits it on its own.
    /// </summary>
    /// <typeparam name="T">The data shape.</typeparam>
    /// <param name="mutate">
    /// Receives the stored data, or a new instance when there is none. Returns whether it changed
    /// anything; when it returns <see langword="false"/> nothing is written.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="UserPersistenceException">Thrown when there is no current user, or the change could not be committed.</exception>
    Task UpdateAsync<T>(Func<T, bool> mutate, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Discards any cached copy of the current user, so the next read sees what other requests have
    /// committed since.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
