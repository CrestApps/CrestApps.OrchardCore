using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterMetricsProjectionMaintenanceService"/>.
/// </summary>
public sealed class ContactCenterMetricsProjectionMaintenanceService : IContactCenterMetricsProjectionMaintenanceService
{
    private const int PageSize = 500;

    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterMetricStore _metricStore;
    private readonly IContactCenterMetricDeltaStore _deltaStore;
    private readonly IContactCenterProjectionCheckpointStore _checkpointStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricsProjectionMaintenanceService"/> class.
    /// </summary>
    /// <param name="eventStore">The source-of-truth event log store.</param>
    /// <param name="metricStore">The daily metric projection store.</param>
    /// <param name="deltaStore">The store holding contributions that have not been folded into the totals yet.</param>
    /// <param name="checkpointStore">The projection replay checkpoint store.</param>
    /// <param name="clock">The clock used to stamp metric and checkpoint times.</param>
    public ContactCenterMetricsProjectionMaintenanceService(
        IInteractionEventStore eventStore,
        IContactCenterMetricStore metricStore,
        IContactCenterMetricDeltaStore deltaStore,
        IContactCenterProjectionCheckpointStore checkpointStore,
        IClock clock)
    {
        _eventStore = eventStore;
        _metricStore = metricStore;
        _deltaStore = deltaStore;
        _checkpointStore = checkpointStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var recomputed = await RecomputeAsync(cancellationToken);

        // A contribution that is still waiting is a count the log already reports and the totals do not, so it is
        // subtracted here and added back when it is folded. That is what stops a rebuild from counting it twice.
        //
        // The contributions are read after the log rather than before, because folding them first, or reading
        // them first, makes an event recorded between the two reads counted by the recompute and folded again
        // on top of it. Reading the log first removes that case.
        //
        // It does not make the rebuild exact, and the remaining cases are not all in one direction. A rebuild
        // cannot read the log, the contributions and the totals in one snapshot, and two of the gaps it cannot
        // close leave a total that is high rather than short. A contribution is appended by the projection
        // handler, which runs in a post-commit scope and is redelivered by the outbox, so an event is in the log
        // for a window before its contribution exists at all: the recompute counts that event, nothing is
        // subtracted for it, and folding the contribution afterwards adds it a second time. Document
        // identifiers are also allocated before the transaction that commits them, so a contribution can become
        // visible below a position the walk has already passed and is missed for the same reason. Both settle by
        // themselves in the sense that a rebuild run once the projection is settled — the outbox drained and the
        // roller caught up — writes exactly the log; neither is silent, because the next drift check reports the
        // difference. A rebuild run against live traffic is a repair that converges, not a snapshot that is
        // exact, and the difference matters to an operator reading the number immediately afterwards.
        var pending = await ReadPendingContributionsAsync(cancellationToken);

        var stored = await _metricStore.ListAllAsync(cancellationToken);
        var remaining = stored.ToDictionary(metric => (metric.DateKey, metric.EventType));

        var changes = 0;
        var now = _clock.UtcNow;

        foreach (var bucket in recomputed.Counts)
        {
            var target = Math.Max(0, bucket.Value - pending.GetValueOrDefault(bucket.Key));

            if (remaining.TryGetValue(bucket.Key, out var metric))
            {
                remaining.Remove(bucket.Key);

                if (metric.Count != target)
                {
                    metric.Count = target;
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
                Date = ContactCenterMetricDateKey.Parse(bucket.Key.DateKey),
                EventType = bucket.Key.EventType,
                Count = target,
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

        // A contribution that has not been folded yet is part of the projection, so it is added to the stored
        // total before the comparison. Detecting drift stays a read: it reports what the projection holds, it
        // does not repair it, and it must not commit the ambient unit of work of whoever asked.
        var pending = await ReadPendingContributionsAsync(cancellationToken);

        foreach (var contribution in pending)
        {
            storedByKey[contribution.Key] = storedByKey.GetValueOrDefault(contribution.Key) + contribution.Value;
        }

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

    private async Task<Dictionary<(string DateKey, string EventType), long>> ReadPendingContributionsAsync(CancellationToken cancellationToken)
    {
        var pending = new Dictionary<(string DateKey, string EventType), long>();
        var afterDocumentId = 0L;

        while (true)
        {
            var page = await _deltaStore.ListContributionsAfterAsync(afterDocumentId, PageSize, cancellationToken);

            if (page.Count == 0)
            {
                break;
            }

            foreach (var contribution in page)
            {
                var key = (contribution.DateKey, contribution.EventType);

                pending[key] = pending.GetValueOrDefault(key) + contribution.Count;
                afterDocumentId = Math.Max(afterDocumentId, contribution.DocumentId);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        return pending;
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

                var dateKey = ContactCenterMetricDateKey.From(interactionEvent.OccurredUtc);
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

    private sealed record RecomputeResult(
        Dictionary<(string DateKey, string EventType), long> Counts,
        DateTime LastOccurredUtc,
        string LastEventId);
}
