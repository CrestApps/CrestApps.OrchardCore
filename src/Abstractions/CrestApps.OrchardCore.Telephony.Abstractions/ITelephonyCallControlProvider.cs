using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the call-control operations a telephony provider supports. A provider that cannot place or
/// end calls simply does not implement this contract, and the soft phone refuses those operations.
/// </summary>
public interface ITelephonyCallControlProvider
{
    /// <summary>
    /// Places an outbound call.
    /// </summary>
    /// <param name="request">The dial request describing the destination and caller identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the placed call or the failure reason.</returns>
    Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends an active call.
    /// </summary>
    /// <param name="call">A reference to the call to end.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default);
}
