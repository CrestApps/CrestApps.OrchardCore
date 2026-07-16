using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterMetricsService"/>.
/// </summary>
public sealed class ContactCenterMetricsService : IContactCenterMetricsService
{
    private const string DateKeyFormat = "yyyy-MM-dd";

    private readonly IContactCenterMetricStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricsService"/> class.
    /// </summary>
    /// <param name="store">The metric store.</param>
    /// <param name="clock">The clock used to stamp metric times.</param>
    public ContactCenterMetricsService(
        IContactCenterMetricStore store,
        IClock clock)
    {
        _store = store;
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
        var dateKey = date.ToString(DateKeyFormat, CultureInfo.InvariantCulture);

        var metric = await _store.FindAsync(dateKey, eventType, cancellationToken);

        if (metric is null)
        {
            metric = new ContactCenterEventMetric
            {
                ItemId = IdGenerator.GenerateId(),
                DateKey = dateKey,
                Date = date,
                EventType = eventType,
                Count = 1,
                CreatedUtc = _clock.UtcNow,
            };

            await _store.CreateAsync(metric, cancellationToken);

            return;
        }

        metric.Count++;
        metric.ModifiedUtc = _clock.UtcNow;
        await _store.UpdateAsync(metric, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, long>> GetSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var metrics = await _store.ListByDateRangeAsync(
            fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            cancellationToken);

        return metrics
            .GroupBy(metric => metric.EventType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(metric => metric.Count), StringComparer.Ordinal);
    }
}
