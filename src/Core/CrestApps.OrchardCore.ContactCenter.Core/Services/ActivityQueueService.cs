using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityQueueService"/>.
/// </summary>
public sealed class ActivityQueueService : IActivityQueueService
{
    private const int MaxEnqueueAttempts = 3;

    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IBusinessHoursService _businessHours;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ISession _session;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="businessHours">The business-hours service used to evaluate after-hours overflow.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="session">The YesSql session used to make newly queued work visible to immediate routing queries.</param>
    /// <param name="scopeExecutor">The executor used to retry idempotent enqueue conflicts in a fresh scope.</param>
    /// <param name="clock">The clock used to stamp queue times.</param>
    public ActivityQueueService(
        IQueueItemManager queueItemManager,
        IActivityQueueManager queueManager,
        IOmnichannelActivityManager activityManager,
        IBusinessHoursService businessHours,
        IContactCenterEventPublisher publisher,
        ISession session,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock)
    {
        _queueItemManager = queueItemManager;
        _queueManager = queueManager;
        _activityManager = activityManager;
        _businessHours = businessHours;
        _publisher = publisher;
        _session = session;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<QueueItem> EnqueueAsync(string activityItemId, string queueId, InteractionPriority? priority, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        try
        {
            return await EnqueueCoreAsync(activityItemId, queueId, priority, cancellationToken);
        }
        catch (Exception exception) when (IsEnqueueConflict(exception))
        {
            return await RetryEnqueueInFreshScopeAsync(activityItemId, queueId, priority, cancellationToken);
        }
    }

    private async Task<QueueItem> EnqueueCoreAsync(
        string activityItemId,
        string queueId,
        InteractionPriority? priority,
        CancellationToken cancellationToken)
    {
        var existing = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (existing is not null && existing.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            return existing;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);
        var item = await _queueItemManager.NewAsync(cancellationToken: cancellationToken);
        item.QueueId = queueId;
        item.ActivityItemId = activityItemId;
        item.Priority = priority ?? queue?.DefaultPriority ?? InteractionPriority.Normal;
        item.Status = QueueItemStatus.Waiting;
        item.StickyAgentUserId = activity?.AssignedToId;
        item.EnqueuedUtc = _clock.UtcNow;
        item.QueueEnteredUtc = item.EnqueuedUtc;

        await _queueItemManager.CreateAsync(item, cancellationToken: cancellationToken);

        if (activity is not null)
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Available;
            await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemAdded,
            AggregateType = nameof(QueueItem),
            AggregateId = item.ItemId,
            SourceComponent = ContactCenterConstants.Components.Queues,
        }, cancellationToken);

        return item;
    }

    private async Task<QueueItem> RetryEnqueueInFreshScopeAsync(
        string activityItemId,
        string queueId,
        InteractionPriority? priority,
        CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 2; attempt <= MaxEnqueueAttempts; attempt++)
        {
            QueueItem item = null;

            try
            {
                await _scopeExecutor.ExecuteAsync<IActivityQueueService>(async queueService =>
                {
                    item = await queueService.EnqueueAsync(activityItemId, queueId, priority, cancellationToken);
                });

                return item;
            }
            catch (Exception exception) when (IsEnqueueConflict(exception))
            {
                lastException = exception;
            }
        }

        throw lastException ?? new ConcurrencyException(new Document());
    }

    private static bool IsEnqueueConflict(Exception exception)
    {
        return exception is ConcurrencyException or DbException;
    }

    /// <inheritdoc/>
    public async Task DequeueAsync(QueueItem queueItem, QueueItemStatus status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        queueItem.Status = status;
        queueItem.DequeuedUtc = _clock.UtcNow;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemDequeued,
            AggregateType = nameof(QueueItem),
            AggregateId = queueItem.ItemId,
            SourceComponent = ContactCenterConstants.Components.Queues,
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> OverflowDueAsync(ActivityQueue queue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (string.IsNullOrEmpty(queue.OverflowQueueId) ||
            string.Equals(queue.OverflowQueueId, queue.ItemId, StringComparison.Ordinal))
        {
            return 0;
        }

        var waiting = await _queueItemManager.ListWaitingAsync(queue.ItemId, cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;

        var closed = !string.IsNullOrEmpty(queue.BusinessHoursCalendarId)
            && queue.AfterHoursAction == QueueAfterHoursAction.Overflow
            && !await _businessHours.IsOpenAsync(queue.BusinessHoursCalendarId, now, cancellationToken);

        var moved = 0;

        foreach (var item in waiting)
        {
            var queueEnteredUtc = item.QueueEnteredUtc == default
                ? item.EnqueuedUtc
                : item.QueueEnteredUtc;
            var overflowDueByWait = queue.OverflowAfterSeconds > 0
                && (now - queueEnteredUtc).TotalSeconds >= queue.OverflowAfterSeconds;

            if (!closed && !overflowDueByWait)
            {
                continue;
            }

            if (item.OverflowHistory.Contains(queue.OverflowQueueId, StringComparer.Ordinal))
            {
                continue;
            }

            if (!item.OverflowHistory.Contains(queue.ItemId, StringComparer.Ordinal))
            {
                item.OverflowHistory.Add(queue.ItemId);
            }

            item.OverflowedFromQueueId = queue.ItemId;
            item.QueueId = queue.OverflowQueueId;
            item.QueueEnteredUtc = now;
            await _queueItemManager.UpdateAsync(item, cancellationToken: cancellationToken);

            await _publisher.PublishAsync(new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.QueueItemOverflowed,
                AggregateType = nameof(QueueItem),
                AggregateId = item.ItemId,
                SourceComponent = ContactCenterConstants.Components.Queues,
            }, cancellationToken);

            moved++;
        }

        return moved;
    }
}