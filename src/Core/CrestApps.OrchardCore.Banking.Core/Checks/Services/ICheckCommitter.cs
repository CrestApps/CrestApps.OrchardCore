namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Commits the check register's pending writes, so a change made under a lock is stored before the lock is released.
/// </summary>
public interface ICheckCommitter
{
    /// <summary>
    /// Commits the pending writes.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
