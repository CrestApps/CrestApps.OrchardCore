using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides reservation and queue compensation for outbound dial attempts.
/// </summary>
public sealed class DialerAttemptCompensationService : IDialerAttemptCompensationService
{
    private readonly IActivityReservationService _reservationService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptCompensationService"/> class.
    /// </summary>
    /// <param name="reservationService">The reservation service.</param>
    /// <param name="queueItemManager">The queue item manager, used to send a record that cannot be dialed yet to the back of the queue.</param>
    /// <param name="clock">The clock.</param>
    public DialerAttemptCompensationService(
        IActivityReservationService reservationService,
        IQueueItemManager queueItemManager,
        IClock clock)
    {
        _reservationService = reservationService;
        _queueItemManager = queueItemManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task CompensateAsync(
        ActivityReservation reservation,
        bool removeFromQueue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        await _reservationService.CompensateAsync(
            reservation.ItemId,
            removeFromQueue,
            cancellationToken);

        if (!removeFromQueue)
        {
            await MoveToBackAsync(reservation.QueueItemId, cancellationToken);
        }
    }

    // A record kept in the queue could not be dialed yet: it is cooling down after an unanswered attempt, or the
    // contact is outside their calling window. It went back with its old place in line, so the dialer picked the same
    // record on every cycle, suppressed it again, and never reached the rest of the campaign. It goes to the back
    // instead, and the next record gets its turn.
    private async Task MoveToBackAsync(string queueItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueItemId))
        {
            return;
        }

        var queueItem = await _queueItemManager.FindByIdAsync(queueItemId, cancellationToken);

        if (queueItem is null ||
            queueItem.Status != QueueItemStatus.Waiting ||
            !string.IsNullOrEmpty(queueItem.ReservationId))
        {
            return;
        }

        queueItem.EnqueuedUtc = _clock.UtcNow;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
    }
}
