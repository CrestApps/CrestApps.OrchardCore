using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Places a single compliant outbound dialing attempt for a reserved activity. The attempt service is
/// the only path that runs the compliance gate, records communication-history interactions, routes the
/// call through the Voice Contact Center Call Router, and audits suppressed attempts.
/// </summary>
public interface IDialerAttemptService
{
    /// <summary>
    /// Attempts to dial the reserved activity, applying the compliance gate before placing the call.
    /// </summary>
    /// <param name="profile">The dialer profile that governs the attempt.</param>
    /// <param name="reservation">The reservation that pairs the activity with an agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when an outbound call was started; otherwise <see langword="false"/>.</returns>
    Task<bool> TryDialAsync(DialerProfile profile, ActivityReservation reservation, CancellationToken cancellationToken = default);
}
