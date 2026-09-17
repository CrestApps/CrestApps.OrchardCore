namespace CrestApps.Core.Locking;

/// <summary>
/// Acquires mutual-exclusion locks that are honoured across every node running the application.
/// </summary>
/// <remarks>
/// The members mirror the subset of lock operations the Contact Center Suite actually performs, and
/// keep the parameter order of the host lock they most often wrap, so an adapter is a one-line
/// delegation and a call site changes only the type it injects.
/// </remarks>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Tries to acquire the lock, giving up once <paramref name="timeout"/> elapses.
    /// </summary>
    /// <param name="key">The key identifying the lock.</param>
    /// <param name="timeout">How long to wait for the lock. <see cref="TimeSpan.Zero"/> means do not wait.</param>
    /// <param name="expiration">How long the lock is held before it expires on its own. <see langword="null"/> means it never expires.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The locker and whether it was acquired. The locker is disposed either way; when
    /// <c>Locked</c> is <see langword="false"/> the caller did not get the lock and must not do the guarded work.
    /// </returns>
    Task<(ILocker Locker, bool Locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires the lock, waiting as long as it takes.
    /// </summary>
    /// <param name="key">The key identifying the lock.</param>
    /// <param name="expiration">How long the lock is held before it expires on its own. <see langword="null"/> means it never expires.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired locker, which the caller disposes to release the lock.</returns>
    Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the lock is currently held by anyone.
    /// </summary>
    /// <param name="key">The key identifying the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the lock is held.</returns>
    Task<bool> IsLockAcquiredAsync(string key, CancellationToken cancellationToken = default);
}
