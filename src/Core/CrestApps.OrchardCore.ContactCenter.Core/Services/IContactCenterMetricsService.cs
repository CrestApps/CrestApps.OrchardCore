namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Records and reports the daily Contact Center event-count projection used for operational and
/// historical reporting.
/// </summary>
public interface IContactCenterMetricsService
{
    /// <summary>
    /// Increments the daily count for the specified event type on the day the event occurred.
    /// </summary>
    /// <param name="eventType">The domain event type.</param>
    /// <param name="occurredUtc">The UTC time the event occurred.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordAsync(string eventType, DateTime occurredUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count of each event type over the inclusive UTC date range.
    /// </summary>
    /// <param name="fromDate">The inclusive lower UTC date.</param>
    /// <param name="toDate">The inclusive upper UTC date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A dictionary of event type to total count.</returns>
    Task<IReadOnlyDictionary<string, long>> GetSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
