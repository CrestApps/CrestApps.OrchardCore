using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for unrolled daily event count contributions.
/// </summary>
public interface IContactCenterMetricDeltaStore : ICatalog<ContactCenterEventMetricDelta>
{
    /// <summary>
    /// Lists a bounded batch of contributions so the roller never has to hold the whole table in memory. No
    /// order is requested: the roller sums whatever it is handed and removes exactly those rows, so the order
    /// carries no meaning, and asking for one would make the engine sort the entire backlog before it could
    /// return a single batch.
    /// </summary>
    /// <param name="maxCount">The maximum number of contributions to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The batch of contributions.</returns>
    Task<IReadOnlyList<ContactCenterEventMetricDelta>> ListBatchAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the contributions whose day falls within the inclusive range. A reader has to add these to the
    /// rolled-up totals, because a contribution that has not been folded yet is still a real event.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC date.</param>
    /// <param name="toUtc">The inclusive upper UTC date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The contributions in the range.</returns>
    Task<IReadOnlyCollection<ContactCenterEventMetricDelta>> ListByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the contributions positioned after the supplied document identifier, in document order, so a
    /// caller that has to account for every one of them can walk the whole table. The walk resumes from a
    /// position rather than from an offset because the roller deletes the rows it folds from anywhere in the
    /// table: an offset would step over rows that are still waiting once earlier ones are gone, and those
    /// counts would be missed with nothing to show for it. The walk removes that skew; it does not turn the
    /// pages into one snapshot. Identifiers are allocated before the transaction that commits them, so a
    /// contribution can still become visible below a position the walk has already passed, and a caller that
    /// has to be exact has to account for that separately. The contributions are read from the index alone,
    /// without loading the documents they belong to.
    /// </summary>
    /// <param name="afterDocumentId">The document identifier to resume after; zero starts from the beginning.</param>
    /// <param name="count">The maximum number of contributions to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The contributions after the supplied position.</returns>
    Task<IReadOnlyList<ContactCenterMetricContribution>> ListContributionsAfterAsync(long afterDocumentId, int count, CancellationToken cancellationToken = default);
}
