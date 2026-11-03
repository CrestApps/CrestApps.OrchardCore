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

    /// <summary>
    /// Attempts to dial the reserved activity like <see cref="TryDialAsync"/>, and also reports why the compliance gate
    /// refused it, so the agent who asked for the call can be told.
    /// </summary>
    /// <param name="profile">The dialer profile that governs the attempt.</param>
    /// <param name="reservation">The reservation that pairs the activity with an agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the attempt.</returns>
    Task<DialerAttemptResult> DialAsync(DialerProfile profile, ActivityReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the compliance gate for a reserved record before it is offered to an agent, and closes the record without
    /// offering it when it can never be dialed, such as a number on a do-not-call registry. A record that is only not
    /// callable yet is left alone.
    /// </summary>
    /// <param name="profile">The dialer profile that governs the record.</param>
    /// <param name="reservation">The reservation that pairs the record with an agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the record was closed and its reservation released; otherwise <see langword="false"/>.</returns>
    Task<bool> TrySuppressUndialableAsync(DialerProfile profile, ActivityReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to dial a waiting campaign item without reserving an agent for it, for an over-dialing Predictive
    /// profile. The item becomes Assigned with no agent, the compliance gate runs first, and the call is placed once the
    /// caller's transaction commits; an agent is claimed for it only when a person answers.
    /// </summary>
    /// <param name="profile">The over-dialing Predictive profile that governs the attempt.</param>
    /// <param name="queueItem">The waiting queue item to dial.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the call was staged to be placed; otherwise <see langword="false"/>.</returns>
    Task<bool> TryDialUnreservedAsync(DialerProfile profile, QueueItem queueItem, CancellationToken cancellationToken = default);
}
