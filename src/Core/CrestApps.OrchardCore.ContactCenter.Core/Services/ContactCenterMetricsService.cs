using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterMetricsService"/>.
/// </summary>
public sealed class ContactCenterMetricsService : IContactCenterMetricsService
{
    private readonly IContactCenterMetricStore _store;
    private readonly IContactCenterMetricDeltaStore _deltaStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricsService"/> class.
    /// </summary>
    /// <param name="store">The metric store holding the rolled-up daily totals.</param>
    /// <param name="deltaStore">The store the individual contributions are appended to.</param>
    /// <param name="clock">The clock used to stamp metric times.</param>
    public ContactCenterMetricsService(
        IContactCenterMetricStore store,
        IContactCenterMetricDeltaStore deltaStore,
        IClock clock)
    {
        _store = store;
        _deltaStore = deltaStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(string eventType, DateTime occurredUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(eventType))
        {
            return;
        }

        var effectiveUtc = occurredUtc == default ? _clock.UtcNow : occurredUtc;
        var date = effectiveUtc.Date;

        // The contribution is appended rather than added to the day's total in place. Reading the total,
        // incrementing it and writing it back makes that one row a serialization point at any real event rate:
        // every writer of the same event type on the same day contends for it, and under the store's optimistic
        // concurrency the loser either fails the whole request or overwrites a count it never read. An append
        // has no reader to be stale, no row to contend for, and no constraint two writers can both violate.
        await _deltaStore.CreateAsync(
            new ContactCenterEventMetricDelta
            {
                ItemId = IdGenerator.GenerateId(),
                DateKey = ContactCenterMetricDateKey.From(date),
                Date = date,
                EventType = eventType,
                Count = 1,
                CreatedUtc = _clock.UtcNow,
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, long>> GetSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var metrics = await _store.ListByDateRangeAsync(fromUtc, toUtc, cancellationToken);

        var summary = metrics
            .GroupBy(metric => metric.EventType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(metric => metric.Count), StringComparer.Ordinal);

        // Contributions that have not been folded yet are still real events, so a reader adds them to the
        // totals. Without this a summary read a moment after the traffic it describes would report a number
        // that is behind by however much the roller has not caught up on.
        var pending = await _deltaStore.ListByDateRangeAsync(fromUtc, toUtc, cancellationToken);

        foreach (var group in pending.GroupBy(delta => delta.EventType, StringComparer.Ordinal))
        {
            summary[group.Key] = summary.GetValueOrDefault(group.Key) + group.Sum(delta => delta.Count);
        }

        return summary;
    }
}
