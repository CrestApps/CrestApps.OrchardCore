using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterMetricRollupService"/>.
/// </summary>
public sealed class ContactCenterMetricRollupService : IContactCenterMetricRollupService
{
    private const int BatchSize = 500;
    private const int MaxBatchesPerRun = 20;

    private readonly IContactCenterMetricDeltaStore _deltaStore;
    private readonly IContactCenterMetricStore _metricStore;
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricRollupService"/> class.
    /// </summary>
    /// <param name="deltaStore">The store holding the appended contributions.</param>
    /// <param name="metricStore">The store holding the daily totals.</param>
    /// <param name="session">The YesSql session, used to commit each batch on its own.</param>
    /// <param name="timeProvider">The time provider used to stamp the totals.</param>
    public ContactCenterMetricRollupService(
        IContactCenterMetricDeltaStore deltaStore,
        IContactCenterMetricStore metricStore,
        ISession session,
        TimeProvider timeProvider)
    {
        _deltaStore = deltaStore;
        _metricStore = metricStore;
        _session = session;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<int> RollupAsync(CancellationToken cancellationToken = default)
    {
        var folded = 0;

        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var deltas = await _deltaStore.GetBatchAsync(BatchSize, cancellationToken);

            if (deltas.Count == 0)
            {
                break;
            }

            foreach (var group in deltas.GroupBy(delta => (delta.DateKey, delta.EventType)))
            {
                await AddAsync(group.Key.DateKey, group.Key.EventType, group.Sum(delta => delta.Count), cancellationToken);
            }

            // Only the contributions this batch actually read are removed. Deleting by predicate instead would
            // also remove anything appended between the read and the delete, which would be counted by nobody.
            foreach (var delta in deltas)
            {
                await _deltaStore.DeleteAsync(delta, cancellationToken);
            }

            // Each batch is committed on its own so a long fold never accumulates one unbounded transaction,
            // and so an interrupted run keeps the batches it had already folded.
            await _session.SaveChangesAsync(cancellationToken);

            folded += deltas.Count;

            if (deltas.Count < BatchSize)
            {
                break;
            }
        }

        return folded;
    }

    private async Task AddAsync(string dateKey, string eventType, long count, CancellationToken cancellationToken)
    {
        var metric = await _metricStore.FindAsync(dateKey, eventType, cancellationToken);

        if (metric is null)
        {
            await _metricStore.CreateAsync(
                new ContactCenterEventMetric
                {
                    ItemId = IdGenerator.GenerateId(),
                    DateKey = dateKey,
                    Date = ContactCenterMetricDateKey.Parse(dateKey),
                    EventType = eventType,
                    Count = count,
                    CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
                },
                cancellationToken);

            return;
        }

        metric.Count += count;
        metric.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _metricStore.UpdateAsync(metric, cancellationToken);
    }
}
