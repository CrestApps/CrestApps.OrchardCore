using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the inbound-call operations a telephony provider supports. A provider that only places
/// outbound calls does not implement this contract.
/// </summary>
public interface ITelephonyInboundCallProvider
{
    /// <summary>
    /// Answers a ringing inbound call.
    /// </summary>
    /// <param name="call">A reference to the inbound call to answer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a ringing inbound call.
    /// </summary>
    /// <param name="call">A reference to the inbound call to reject.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default);
}
