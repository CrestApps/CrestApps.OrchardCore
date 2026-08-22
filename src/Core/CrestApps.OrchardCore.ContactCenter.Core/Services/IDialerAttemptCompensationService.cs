using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Compensates an outbound dial attempt by releasing its reservation and optionally removing its queue item.
/// </summary>
public interface IDialerAttemptCompensationService
{
    /// <summary>
    /// Releases the reservation and optionally removes the associated queue item.
    /// </summary>
    /// <param name="reservation">The reservation to release.</param>
    /// <param name="removeFromQueue">Whether to remove the associated queue item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CompensateAsync(
        ActivityReservation reservation,
        bool removeFromQueue,
        CancellationToken cancellationToken = default);
}
