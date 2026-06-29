using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityQueueService"/>.
/// </summary>
public sealed class ActivityQueueService : IActivityQueueService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp queue times.</param>
    public ActivityQueueService(
        IQueueItemManager queueItemManager,
        IActivityQueueManager queueManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _queueItemManager = queueItemManager;
        _queueManager = queueManager;
        _activityManager = activityManager;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<QueueItem> EnqueueAsync(string activityItemId, string queueId, InteractionPriority? priority, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var existing = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (existing is not null && existing.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            return existing;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var item = await _queueItemManager.NewAsync(cancellationToken: cancellationToken);
        item.QueueId = queueId;
        item.ActivityItemId = activityItemId;
        item.Priority = priority ?? queue?.DefaultPriority ?? InteractionPriority.Normal;
        item.Status = QueueItemStatus.Waiting;
        item.EnqueuedUtc = _clock.UtcNow;

        await _queueItemManager.CreateAsync(item, cancellationToken: cancellationToken);

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is not null)
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Available;
            await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
        }

        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemAdded,
            AggregateType = nameof(QueueItem),
            AggregateId = item.ItemId,
            SourceComponent = ContactCenterConstants.Components.Queues,
        }, cancellationToken);

        return item;
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
}
