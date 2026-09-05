using CrestApps.OrchardCore.ContactCenter.Core.Models;
namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides reservation and queue compensation for outbound dial attempts.
/// </summary>
public sealed class DialerAttemptCompensationService : IDialerAttemptCompensationService
{
    private readonly IActivityReservationService _reservationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptCompensationService"/> class.
    /// </summary>
    /// <param name="reservationService">The reservation service.</param>
    public DialerAttemptCompensationService(IActivityReservationService reservationService)
    {
        _reservationService = reservationService;
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
    }
}
