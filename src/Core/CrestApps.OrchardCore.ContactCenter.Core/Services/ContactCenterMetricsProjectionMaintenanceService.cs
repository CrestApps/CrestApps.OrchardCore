using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterMetricsProjectionMaintenanceService"/>.
/// </summary>
public sealed class ContactCenterMetricsProjectionMaintenanceService : IContactCenterMetricsProjectionMaintenanceService
{
    private const string DateKeyFormat = "yyyy-MM-dd";
    private const int PageSize = 500;

    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterMetricStore _metricStore;
    private readonly IContactCenterProjectionCheckpointStore _checkpointStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricsProjectionMaintenanceService"/> class.
    /// </summary>
    /// <param name="eventStore">The source-of-truth event log store.</param>
    /// <param name="metricStore">The daily metric projection store.</param>
    /// <param name="checkpointStore">The projection replay checkpoint store.</param>
    /// <param name="clock">The clock used to stamp metric and checkpoint times.</param>
    public ContactCenterMetricsProjectionMaintenanceService(
        IInteractionEventStore eventStore,
        IContactCenterMetricStore metricStore,
        IContactCenterProjectionCheckpointStore checkpointStore,
        IClock clock)
    {
        _eventStore = eventStore;
        _metricStore = metricStore;
        _checkpointStore = checkpointStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var recomputed = await RecomputeAsync(cancellationToken);

        var stored = await _metricStore.ListAllAsync(cancellationToken);
        var remaining = stored.ToDictionary(metric => (metric.DateKey, metric.EventType));

        var changes = 0;
        var now = _clock.UtcNow;

        foreach (var bucket in recomputed.Counts)
        {
            if (remaining.TryGetValue(bucket.Key, out var metric))
            {
                remaining.Remove(bucket.Key);

                if (metric.Count != bucket.Value)
                {
                    metric.Count = bucket.Value;
                    metric.ModifiedUtc = now;
                    await _metricStore.UpdateAsync(metric, cancellationToken);
                    changes++;
                }

                continue;
            }

            var created = new ContactCenterEventMetric
            {
                ItemId = IdGenerator.GenerateId(),
                DateKey = bucket.Key.DateKey,
                Date = ParseDateKey(bucket.Key.DateKey),
                EventType = bucket.Key.EventType,
                Count = bucket.Value,
                CreatedUtc = now,
            };

            await _metricStore.CreateAsync(created, cancellationToken);
            changes++;
        }

        foreach (var orphan in remaining.Values)
        {
            await _metricStore.DeleteAsync(orphan, cancellationToken);
            changes++;
        }

        await AdvanceCheckpointAsync(recomputed, now, cancellationToken);

        return changes;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactCenterProjectionDrift>> DetectDriftAsync(CancellationToken cancellationToken = default)
    {
        var recomputed = await RecomputeAsync(cancellationToken);

        var stored = await _metricStore.ListAllAsync(cancellationToken);
        var storedByKey = stored.ToDictionary(metric => (metric.DateKey, metric.EventType), metric => metric.Count);

        var drifts = new List<ContactCenterProjectionDrift>();

        foreach (var bucket in recomputed.Counts)
        {
            var actual = storedByKey.GetValueOrDefault(bucket.Key);

            if (actual != bucket.Value)
            {
                drifts.Add(new ContactCenterProjectionDrift
                {
                    DateKey = bucket.Key.DateKey,
                    EventType = bucket.Key.EventType,
                    ExpectedCount = bucket.Value,
                    ActualCount = actual,
                });
            }
        }

        foreach (var entry in storedByKey)
        {
            if (!recomputed.Counts.ContainsKey(entry.Key))
            {
                drifts.Add(new ContactCenterProjectionDrift
                {
                    DateKey = entry.Key.DateKey,
                    EventType = entry.Key.EventType,
                    ExpectedCount = 0,
                    ActualCount = entry.Value,
                });
            }
        }

        return drifts;
    }

    private async Task<RecomputeResult> RecomputeAsync(CancellationToken cancellationToken)
    {
        var counts = new Dictionary<(string DateKey, string EventType), long>();
        var lastOccurredUtc = default(DateTime);
        var lastEventId = string.Empty;

        var skip = 0;

        while (true)
        {
            var page = await _eventStore.ListOrderedPageAsync(skip, PageSize, cancellationToken);

            if (page.Count == 0)
            {
                break;
            }

            foreach (var interactionEvent in page)
            {
                lastOccurredUtc = interactionEvent.OccurredUtc;
                lastEventId = interactionEvent.ItemId;

                // Mirror the live projection: events without a type are not counted, and events without a
                // real occurrence time are skipped because the live path substitutes the wall clock, which
                // cannot be reproduced deterministically during a replay.
                if (string.IsNullOrEmpty(interactionEvent.EventType) || interactionEvent.OccurredUtc == default)
                {
                    continue;
                }

                var dateKey = interactionEvent.OccurredUtc.Date.ToString(DateKeyFormat, CultureInfo.InvariantCulture);
                var key = (dateKey, interactionEvent.EventType);

                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            if (page.Count < PageSize)
            {
                break;
            }

            skip += page.Count;
        }

        return new RecomputeResult(counts, lastOccurredUtc, lastEventId);
    }

    private async Task AdvanceCheckpointAsync(RecomputeResult recomputed, DateTime rebuiltUtc, CancellationToken cancellationToken)
    {
        var checkpoint = await _checkpointStore.FindByHandlerAsync(ContactCenterConstants.MetricsProjectionHandlerId, cancellationToken);

        if (checkpoint is null)
        {
            checkpoint = new ContactCenterProjectionCheckpoint
            {
                ItemId = IdGenerator.GenerateId(),
                HandlerId = ContactCenterConstants.MetricsProjectionHandlerId,
                Version = ContactCenterConstants.MetricsProjectionVersion,
                LastOccurredUtc = recomputed.LastOccurredUtc,
                LastEventId = recomputed.LastEventId,
                RebuiltUtc = rebuiltUtc,
            };

            await _checkpointStore.CreateAsync(checkpoint, cancellationToken);

            return;
        }

        checkpoint.Version = ContactCenterConstants.MetricsProjectionVersion;
        checkpoint.LastOccurredUtc = recomputed.LastOccurredUtc;
        checkpoint.LastEventId = recomputed.LastEventId;
        checkpoint.RebuiltUtc = rebuiltUtc;

        await _checkpointStore.UpdateAsync(checkpoint, cancellationToken);
    }

    private static DateTime ParseDateKey(string dateKey)
    {
        var date = DateTime.ParseExact(dateKey, DateKeyFormat, CultureInfo.InvariantCulture);

        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    private sealed record RecomputeResult(
        Dictionary<(string DateKey, string EventType), long> Counts,
        DateTime LastOccurredUtc,
        string LastEventId);
}
