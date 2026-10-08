using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Holds each ringing offer's deadline in process, so an offer nobody answers is expired at its deadline rather
/// than whenever the next expiry sweep gets round to it.
/// </summary>
/// <remarks>
/// The deadline is armed when the offer is made and dropped when it is accepted or released; the work it runs
/// re-reads the reservation under its lock, so a deadline that outlives its offer (a lost cancel, a redelivered
/// event) finds it settled and does nothing. Expiring through the reservation service publishes the same release
/// every other path does, so the agent's screens drop the offer, the device leg is hung up and the caller is moved
/// on in the same instant.
/// </remarks>
public sealed class OfferDeadlineEventHandler : IContactCenterEventHandler
{
    private readonly IContactCenterDeadlineScheduler _scheduler;
    private readonly IActivityReservationManager _reservationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfferDeadlineEventHandler"/> class.
    /// </summary>
    /// <param name="scheduler">The in-process deadline scheduler.</param>
    /// <param name="reservationManager">The reservation manager, read for the offer's deadline.</param>
    public OfferDeadlineEventHandler(
        IContactCenterDeadlineScheduler scheduler,
        IActivityReservationManager reservationManager)
    {
        _scheduler = scheduler;
        _reservationManager = reservationManager;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/OfferDeadline/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <summary>
    /// The key an offer's deadline is held under.
    /// </summary>
    /// <param name="reservationId">The reservation.</param>
    public static string GetDeadlineKey(string reservationId) => $"offer:{reservationId}";

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (!string.Equals(interactionEvent.AggregateType, nameof(ActivityReservation), StringComparison.Ordinal) ||
            string.IsNullOrEmpty(interactionEvent.AggregateId))
        {
            return;
        }

        switch (interactionEvent.EventType)
        {
            case ContactCenterConstants.Events.AgentReserved:
                var reservation = await _reservationManager.FindByIdAsync(interactionEvent.AggregateId, cancellationToken);

                if (reservation is null || reservation.Status != ReservationStatus.Pending)
                {
                    return;
                }

                _scheduler.Schedule(GetDeadlineKey(reservation.ItemId), reservation.ExpiresUtc, CreateExpiry(reservation.ItemId));
                break;

            case ContactCenterConstants.Events.QueueItemAssigned:
            case ContactCenterConstants.Events.AgentReleased:
                _scheduler.Cancel(GetDeadlineKey(interactionEvent.AggregateId));
                break;
        }
    }

    /// <summary>
    /// Creates the work that expires the offer at its deadline.
    /// </summary>
    /// <param name="reservationId">The reservation.</param>
    internal static Func<IServiceProvider, CancellationToken, Task<DateTime?>> CreateExpiry(string reservationId)
    {
        return async (services, cancellationToken) =>
        {
            // A feature that is being disabled drains its work; the sweep settles the offer if it comes back.
            using var lease = services.GetRequiredService<IContactCenterFeatureWorkManager>().TryEnter(ContactCenterConstants.Feature.Queues);

            if (lease is null)
            {
                return null;
            }

            return await services.GetRequiredService<IReservationDeadlineExpirer>().ExpireAtDeadlineAsync(reservationId, cancellationToken);
        };
    }
}
