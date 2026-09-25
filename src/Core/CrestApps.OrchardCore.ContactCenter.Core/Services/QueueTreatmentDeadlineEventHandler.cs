using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Arms a queue's treatment deadline whenever somebody starts waiting in it — entering the queue, overflowing into
/// it, or coming back after an offer nobody took — so the welcome and the hold music start when they arrive.
/// </summary>
/// <remarks>
/// The queue-treatment sweep runs once a minute. Left to it, a caller arriving just after a run would sit in silence
/// for up to a minute before hearing anything.
/// </remarks>
public sealed class QueueTreatmentDeadlineEventHandler : IContactCenterEventHandler
{
    private readonly Lazy<IQueueTreatmentDeadlineEnforcer> _enforcer;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityReservationManager _reservationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueTreatmentDeadlineEventHandler"/> class.
    /// </summary>
    /// <param name="enforcer">The enforcer, deferred: the treatment it plays reaches services whose event publisher
    /// needs the outbox that constructs this handler.</param>
    /// <param name="queueItemManager">The queue item manager, read for the queue a caller is waiting in.</param>
    /// <param name="reservationManager">The reservation manager, read for the queue item a released offer returns.</param>
    public QueueTreatmentDeadlineEventHandler(
        Lazy<IQueueTreatmentDeadlineEnforcer> enforcer,
        IQueueItemManager queueItemManager,
        IActivityReservationManager reservationManager)
    {
        _enforcer = enforcer;
        _queueItemManager = queueItemManager;
        _reservationManager = reservationManager;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/QueueTreatmentDeadline/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (string.IsNullOrEmpty(interactionEvent.AggregateId))
        {
            return;
        }

        string queueItemId;

        switch (interactionEvent.EventType)
        {
            case ContactCenterConstants.Events.QueueItemAdded:
            case ContactCenterConstants.Events.QueueItemOverflowed:
                queueItemId = interactionEvent.AggregateId;
                break;

            case ContactCenterConstants.Events.AgentReleased
                when string.Equals(interactionEvent.AggregateType, nameof(ActivityReservation), StringComparison.Ordinal):
                queueItemId = (await _reservationManager.FindByIdAsync(interactionEvent.AggregateId, cancellationToken))?.QueueItemId;
                break;

            default:
                return;
        }

        if (string.IsNullOrEmpty(queueItemId))
        {
            return;
        }

        var item = await _queueItemManager.FindByIdAsync(queueItemId, cancellationToken);

        if (item?.Status != QueueItemStatus.Waiting || string.IsNullOrEmpty(item.QueueId))
        {
            return;
        }

        await _enforcer.Value.ArmAsync(item.QueueId, cancellationToken);
    }
}
