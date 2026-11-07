namespace CrestApps.OrchardCore.Retention;

/// <summary>
/// Describes how one kind of stored record is aged out: when a record is old enough to remove, and how to remove a
/// batch of them. Every module that keeps logs, deliveries, codes or carts declares a policy instead of pruning ad hoc,
/// and one retention task runs them all.
/// </summary>
public interface IRetentionPolicy
{
    /// <summary>
    /// Gets the stable technical name of what the policy removes, for logs and reports.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Computes the time before which records may be removed.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="cutoffUtc">When the method returns <see langword="true"/>, the exclusive UTC cutoff.</param>
    /// <returns><see langword="true"/> when the policy removes records; <see langword="false"/> when they are kept.</returns>
    bool TryGetCutoff(DateTime nowUtc, out DateTime cutoffUtc);

    /// <summary>
    /// Removes at most one batch of records older than the cutoff. The caller commits the batch.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff.</param>
    /// <param name="batchSize">The most records to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many records were removed, zero once nothing is left.</returns>
    /// <exception cref="RetentionBatchException">A record failed after the batch staged work, so the caller discards it.</exception>
    Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default);
}
