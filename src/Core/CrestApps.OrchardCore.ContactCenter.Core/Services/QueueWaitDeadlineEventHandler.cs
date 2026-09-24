using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Arms a waiting caller's next wait deadline whenever they start waiting — entering a queue, overflowing into the
/// next one, or coming back after an offer nobody took — and drops it when they leave.
/// </summary>
/// <remarks>
/// Orchard runs a tenant's background tasks one after another, so the queue-treatment sweep that enforces overflow
/// and maximum wait only sweeps for part of each loop; a caller whose maximum wait fell due between its runs waited
/// on until the next one. The deadline this arms fires when it is due, and the sweep remains the backstop.
/// </remarks>
public sealed class QueueWaitDeadlineEventHandler : IContactCenterEventHandler
{
    private readonly Lazy<IQueueWaitDeadlineEnforcer> _enforcer;
    private readonly IContactCenterDeadlineScheduler _scheduler;
    private readonly IActivityReservationManager _reservationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueWaitDeadlineEventHandler"/> class.
    /// </summary>
    /// <param name="enforcer">The enforcer, deferred: it reaches the queue service, whose event publisher needs the
    /// outbox that constructs this handler.</param>
    /// <param name="scheduler">The in-process deadline scheduler.</param>
    /// <param name="reservationManager">The reservation manager, read for the queue item a released offer returns.</param>
    public QueueWaitDeadlineEventHandler(
        Lazy<IQueueWaitDeadlineEnforcer> enforcer,
        IContactCenterDeadlineScheduler scheduler,
        IActivityReservationManager reservationManager)
    {
        _enforcer = enforcer;
        _scheduler = scheduler;
        _reservationManager = reservationManager;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/QueueWaitDeadline/v1";

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

        switch (interactionEvent.EventType)
        {
            case ContactCenterConstants.Events.QueueItemAdded:
            case ContactCenterConstants.Events.QueueItemOverflowed:
                await _enforcer.Value.ArmAsync(interactionEvent.AggregateId, cancellationToken);
                break;

            case ContactCenterConstants.Events.QueueItemDequeued:
                _scheduler.Cancel(QueueWaitDeadlineEnforcer.GetDeadlineKey(interactionEvent.AggregateId));
                break;

            // An offer nobody took puts the caller back in the queue with their wait still running; one that fell due
            // while the offer rang is due now.
            case ContactCenterConstants.Events.AgentReleased
                when string.Equals(interactionEvent.AggregateType, nameof(ActivityReservation), StringComparison.Ordinal):
                var reservation = await _reservationManager.FindByIdAsync(interactionEvent.AggregateId, cancellationToken);

                if (!string.IsNullOrEmpty(reservation?.QueueItemId))
                {
                    await _enforcer.Value.ArmAsync(reservation.QueueItemId, cancellationToken);
                }

                break;
        }
    }
}
