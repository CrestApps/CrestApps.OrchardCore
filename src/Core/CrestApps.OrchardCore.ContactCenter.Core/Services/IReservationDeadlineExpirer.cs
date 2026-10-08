namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Expires one offer at its deadline. The narrow capability the offer's deadline timer needs, kept apart from
/// <see cref="IActivityReservationService"/> the same way <see cref="IActivityReservationReclaimer"/> is, so the
/// reservation lifecycle contract stays unchanged.
/// </summary>
public interface IReservationDeadlineExpirer
{
    /// <summary>
    /// Expires the reservation when it is still ringing and its deadline has passed, under the same reservation
    /// lock and compare-and-set commit an accept, a decline or the expiry sweep takes, so at most one of them wins.
    /// </summary>
    /// <param name="reservationId">The reservation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>When to look again — the deadline when it has not been reached yet (it was extended, or the clock
    /// the timer ran on was early), or a moment later when another transition held the reservation — or
    /// <see langword="null"/> when the offer has been settled, by this call or by anything else.</returns>
    Task<DateTime?> ExpireAtDeadlineAsync(string reservationId, CancellationToken cancellationToken = default);
}
