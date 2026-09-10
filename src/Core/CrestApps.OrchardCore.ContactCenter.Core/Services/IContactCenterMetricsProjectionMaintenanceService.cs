using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Maintains the daily event-count metrics projection by rebuilding it from the durable event log and by
/// detecting drift between the stored projection and the recomputed truth. It makes the projection
/// self-healing and auditable rather than a write-once side effect of event delivery.
/// </summary>
public interface IContactCenterMetricsProjectionMaintenanceService
{
    /// <summary>
    /// Recomputes every daily metric from the source-of-truth event log and reconciles the stored
    /// projection to match, then advances the replay checkpoint. Missing metrics are created, incorrect
    /// counts are corrected, and metrics with no supporting events are removed. Contributions that have been
    /// appended but not folded into the totals yet are excluded from the reconciled totals, because folding
    /// them later adds them; a rebuild therefore does not count them twice. An event recorded, or a contribution
    /// folded, while the rebuild is running can leave that total briefly short until the next rebuild; the
    /// operation is idempotent and converges on a re-run.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of stored metrics that were created, updated, or deleted.</returns>
    Task<int> RebuildAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes every daily metric from the source-of-truth event log and compares it to the stored
    /// projection without modifying anything, returning every detected discrepancy. Contributions that have
    /// been appended but not folded into the totals yet are counted as part of the projection, so a roller
    /// that is merely behind is not reported as drift.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The detected drifts, or an empty list when the projection is consistent.</returns>
    Task<IReadOnlyList<ContactCenterProjectionDrift>> DetectDriftAsync(CancellationToken cancellationToken = default);
}
