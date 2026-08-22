namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reclaims due reservations on a latency-sensitive path without draining the whole expiry backlog and without
/// waiting on reservations another worker is already transitioning beyond a short, bounded time. This is a narrow,
/// additive capability separate from <see cref="IActivityReservationService"/> so a consumer that only needs
/// opportunistic reclamation (such as offering work to an agent) depends on it alone, and so the reservation
/// lifecycle contract other modules implement stays unchanged.
/// </summary>
public interface IActivityReservationReclaimer
{
    /// <summary>
    /// Reclaims up to a bounded number of the oldest due reservations, using only a short, bounded lock wait so a
    /// reservation another node is already transitioning is skipped promptly rather than awaited. Intended for
    /// latency-sensitive paths that must free stale capacity quickly without draining an unbounded backlog; the
    /// scheduled <see cref="IActivityReservationService.ExpireDueAsync"/> sweep remains the authoritative backstop.
    /// </summary>
    /// <param name="maxReservations">The maximum number of due reservations to examine and reclaim in this pass.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of reservations that were reclaimed.</returns>
    Task<int> ReclaimDueAsync(int maxReservations, CancellationToken cancellationToken = default);
}
