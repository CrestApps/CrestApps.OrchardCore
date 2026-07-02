using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for daily event metrics.
/// </summary>
public interface IContactCenterMetricStore : ICatalog<ContactCenterEventMetric>
{
    /// <summary>
    /// Finds the metric for the specified day and event type.
    /// </summary>
    /// <param name="dateKey">The day key formatted as <c>yyyy-MM-dd</c>.</param>
    /// <param name="eventType">The domain event type.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching metric, or <see langword="null"/> when none exists.</returns>
    Task<ContactCenterEventMetric> FindAsync(string dateKey, string eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the metrics whose day falls within the inclusive range.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC date.</param>
    /// <param name="toUtc">The inclusive upper UTC date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The metrics in the range.</returns>
    Task<IReadOnlyCollection<ContactCenterEventMetric>> ListByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
