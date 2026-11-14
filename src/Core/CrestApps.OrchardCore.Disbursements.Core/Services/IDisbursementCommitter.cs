namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Commits what the disbursement service wrote, while it still holds the lock of a key or a batch, so the next holder
/// reads it.
/// </summary>
public interface IDisbursementCommitter
{
    /// <summary>
    /// Commits the pending writes.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
