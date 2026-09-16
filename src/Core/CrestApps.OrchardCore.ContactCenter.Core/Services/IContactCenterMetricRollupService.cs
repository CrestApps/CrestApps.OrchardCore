namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Folds appended event count contributions into the daily totals they belong to.
/// </summary>
public interface IContactCenterMetricRollupService
{
    /// <summary>
    /// Folds a bounded amount of appended contributions into their daily totals and removes the contributions
    /// that were folded. Only the contributions this call read are removed, so a contribution appended while
    /// the fold is running is left for the next one rather than being discarded uncounted.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of contributions folded.</returns>
    Task<int> RollupAsync(CancellationToken cancellationToken = default);
}
