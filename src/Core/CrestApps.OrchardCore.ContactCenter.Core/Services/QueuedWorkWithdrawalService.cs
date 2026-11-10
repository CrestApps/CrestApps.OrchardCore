using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IQueuedWorkWithdrawalService"/>.
/// </summary>
/// <remarks>
/// <para>
/// What happens to the work depends on how far routing got with it. Waiting work is removed. Work ringing an agent
/// has its offer revoked through <see cref="IActivityReservationService.CompensateAsync"/>, which releases the agent
/// and removes the work in one step, exactly as a suppressed dial does. Work an agent has already accepted is left
/// alone: the agent may be on the call, and pulling the work out from under them would drop a live customer. Their
/// own completion settles it, and the CRM refuses a disposition on an activity that has already finished.
/// </para>
/// <para>
/// Withdrawing takes the queue's assignment lock, the one routing holds while it picks and offers work, so the item
/// cannot be offered while it is being withdrawn. When routing holds the lock the withdrawal is deferred: routing
/// withdraws the item itself before offering it (<see cref="TryWithdrawUnroutableAsync"/>).
/// </para>
/// </remarks>
public sealed class QueuedWorkWithdrawalService : IQueuedWorkWithdrawalService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IActivityReservationService _reservationService;
    private readonly IActivityQueueService _queueService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ContactCenterCoordinationOptions _coordinationOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedWorkWithdrawalService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="reservationManager">The reservation manager used to see whether the work is ringing an agent.</param>
    /// <param name="reservationService">The reservation service that revokes a ringing offer.</param>
    /// <param name="queueService">The queue service that takes waiting work out of its queue.</param>
    /// <param name="activityManager">The CRM activity manager used to tell whether queued work is still routable.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="interactionManager">The interaction manager used to name the call the work carried.</param>
    /// <param name="auditRecorder">The recorder that writes the withdrawal to the audit log.</param>
    /// <param name="distributedLock">The distributed lock used to serialize with routing.</param>
    /// <param name="clock">The clock used to stamp the withdrawal.</param>
    /// <param name="coordinationOptions">The coordination options that time the queue lock.</param>
    /// <param name="logger">The logger.</param>
    public QueuedWorkWithdrawalService(
        IQueueItemManager queueItemManager,
        IActivityReservationManager reservationManager,
        IActivityReservationService reservationService,
        IActivityQueueService queueService,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateService workStateService,
        IInteractionManager interactionManager,
        IContactCenterAuditRecorder auditRecorder,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions,
        ILogger<QueuedWorkWithdrawalService> logger)
    {
        _queueItemManager = queueItemManager;
        _reservationManager = reservationManager;
        _reservationService = reservationService;
        _queueService = queueService;
        _activityManager = activityManager;
        _workStateService = workStateService;
        _interactionManager = interactionManager;
        _auditRecorder = auditRecorder;
        _distributedLock = distributedLock;
        _clock = clock;
        _coordinationOptions = coordinationOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QueuedWorkWithdrawalOutcome> WithdrawAsync(
        string activityItemId,
        string reason,
        ContactCenterActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        var queueItem = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (queueItem is null || queueItem.IsSettled)
        {
            return QueuedWorkWithdrawalOutcome.NothingQueued;
        }

        if (queueItem.Status == QueueItemStatus.Assigned)
        {
            LogLeftWithAgent(queueItem, reason);

            return QueuedWorkWithdrawalOutcome.LeftWithAgent;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            ActivityAssignmentService.GetQueueLockKey(queueItem.QueueId),
            _coordinationOptions.AssignmentLockTimeout,
            _coordinationOptions.AssignmentLockExpiration);

        if (!locked)
        {
            LogDeferred(queueItem, reason);

            return QueuedWorkWithdrawalOutcome.Deferred;
        }

        await using var acquiredLock = locker;

        // Read again under the lock: routing may have offered or settled the item since it was first read.
        queueItem = await _queueItemManager.FindByIdAsync(queueItem.ItemId, cancellationToken);

        if (queueItem is null || queueItem.IsSettled)
        {
            return QueuedWorkWithdrawalOutcome.NothingQueued;
        }

        return queueItem.Status switch
        {
            QueueItemStatus.Waiting => await WithdrawWaitingAsync(queueItem, reason, actor, cancellationToken),
            QueueItemStatus.Reserved => await WithdrawReservedAsync(queueItem, reason, actor, cancellationToken),
            _ => LeftWithAgent(queueItem, reason),
        };
    }

    /// <inheritdoc/>
    public async Task<bool> TryWithdrawUnroutableAsync(QueueItem queueItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        if (queueItem.Status != QueueItemStatus.Waiting || string.IsNullOrEmpty(queueItem.ActivityItemId))
        {
            return false;
        }

        var activity = await _activityManager.FindByIdAsync(queueItem.ActivityItemId, cancellationToken);

        string reason;

        if (activity is null)
        {
            reason = QueuedWorkWithdrawalReasons.ActivityDeleted;
        }
        else if (activity.Status.IsTerminal())
        {
            reason = QueuedWorkWithdrawalReasons.For(activity.Status);
        }
        else
        {
            return false;
        }

        // Routing already holds this queue's assignment lock, so the item is withdrawn in place. Nobody asked for
        // this: the activity ended somewhere that did not tell routing, so the platform cleans up after it.
        await WithdrawWaitingAsync(queueItem, reason, ContactCenterActor.System, cancellationToken);

        return true;
    }

    private async Task<QueuedWorkWithdrawalOutcome> WithdrawWaitingAsync(
        QueueItem queueItem,
        string reason,
        ContactCenterActor actor,
        CancellationToken cancellationToken)
    {
        var data = await CreateEventDataAsync(queueItem, reason, cancellationToken);

        queueItem.ReservationId = null;
        queueItem.AgentId = null;

        await _queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);
        await ReleaseWorkStateAsync(queueItem.ActivityItemId, reason, cancellationToken);
        await _auditRecorder.RecordQueueItemWithdrawnAsync(data, actor, cancellationToken);

        LogWithdrawn(data, actor);

        return QueuedWorkWithdrawalOutcome.Withdrawn;
    }

    private async Task<QueuedWorkWithdrawalOutcome> WithdrawReservedAsync(
        QueueItem queueItem,
        string reason,
        ContactCenterActor actor,
        CancellationToken cancellationToken)
    {
        var reservation = string.IsNullOrEmpty(queueItem.ReservationId)
            ? null
            : await _reservationManager.FindByIdAsync(queueItem.ReservationId, cancellationToken);

        // A reserved item with no live offer behind it is not ringing anybody; it is waiting work in all but name.
        if (reservation is null || reservation.Status is not ReservationStatus.Pending and not ReservationStatus.Accepted)
        {
            return await WithdrawWaitingAsync(queueItem, reason, actor, cancellationToken);
        }

        if (reservation.Status == ReservationStatus.Accepted)
        {
            return LeftWithAgent(queueItem, reason);
        }

        var data = await CreateEventDataAsync(queueItem, reason, cancellationToken);
        data.RevokedReservationId = reservation.ItemId;
        data.AgentId = reservation.AgentId;

        // Compensation revokes the offer, releases the agent, releases the work state and removes the item from the
        // queue together. It commits as it goes, so nothing loaded before it is saved again here.
        var revoked = await _reservationService.CompensateAsync(reservation.ItemId, removeFromQueue: true, cancellationToken);

        if (revoked is null)
        {
            // The agent answered, or the offer expired, while this was deciding. An answered offer stays with the
            // agent; an expired one returns the item to the queue, where routing withdraws it before offering it.
            LogDeferred(queueItem, reason);

            return QueuedWorkWithdrawalOutcome.Deferred;
        }

        await _auditRecorder.RecordQueueItemWithdrawnAsync(data, actor, cancellationToken);

        LogWithdrawn(data, actor);

        return QueuedWorkWithdrawalOutcome.OfferRevoked;
    }

    private async Task<QueueItemWithdrawnEventData> CreateEventDataAsync(QueueItem queueItem, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var interaction = await _interactionManager.FindByActivityIdAsync(queueItem.ActivityItemId, cancellationToken);

        return new QueueItemWithdrawnEventData
        {
            QueueItemId = queueItem.ItemId,
            QueueId = queueItem.QueueId,
            ActivityItemId = queueItem.ActivityItemId,
            InteractionId = interaction?.ItemId,
            PreviousState = queueItem.Status.ToString(),
            Reason = reason,
            WaitSeconds = ContactCenterCallAudit.QueueWaitSeconds(queueItem, now),
            WithdrawnUtc = now,
        };
    }

    private async Task ReleaseWorkStateAsync(string activityItemId, string reason, CancellationToken cancellationToken)
    {
        // A deleted activity has no work state worth keeping, and mutating one would create it afresh.
        if (reason == QueuedWorkWithdrawalReasons.ActivityDeleted)
        {
            return;
        }

        await _workStateService.MutateAsync(activityItemId, workState =>
        {
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;

            if (workState.AssignmentStatus != ActivityAssignmentStatus.Released &&
                workState.CanTransitionTo(ActivityAssignmentStatus.Released))
            {
                workState.TransitionTo(ActivityAssignmentStatus.Released);
            }
        }, cancellationToken);
    }

    private QueuedWorkWithdrawalOutcome LeftWithAgent(QueueItem queueItem, string reason)
    {
        LogLeftWithAgent(queueItem, reason);

        return QueuedWorkWithdrawalOutcome.LeftWithAgent;
    }

    private void LogWithdrawn(QueueItemWithdrawnEventData data, ContactCenterActor actor)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Withdrew Contact Center queue item '{QueueItemId}' for activity '{ActivityItemId}' from queue '{QueueId}' because the activity is no longer routable ({Reason}); it was {PreviousState}, acted on by {ActorType}.",
                data.QueueItemId.SanitizeLogValue(),
                data.ActivityItemId.SanitizeLogValue(),
                data.QueueId.SanitizeLogValue(),
                data.Reason.SanitizeLogValue(),
                data.PreviousState,
                actor?.Type ?? ContactCenterActorType.System);
        }
    }

    private void LogLeftWithAgent(QueueItem queueItem, string reason)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Left Contact Center queue item '{QueueItemId}' for activity '{ActivityItemId}' with its agent although the activity is no longer routable ({Reason}); the agent has already taken it.",
                queueItem.ItemId.SanitizeLogValue(),
                queueItem.ActivityItemId.SanitizeLogValue(),
                reason.SanitizeLogValue());
        }
    }

    private void LogDeferred(QueueItem queueItem, string reason)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Deferred withdrawing Contact Center queue item '{QueueItemId}' for activity '{ActivityItemId}' ({Reason}); routing withdraws it before offering it.",
                queueItem.ItemId.SanitizeLogValue(),
                queueItem.ActivityItemId.SanitizeLogValue(),
                reason.SanitizeLogValue());
        }
    }
}
