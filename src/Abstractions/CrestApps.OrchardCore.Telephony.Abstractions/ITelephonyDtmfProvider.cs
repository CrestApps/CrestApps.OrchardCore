using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the DTMF operations a telephony provider supports.
/// </summary>
public interface ITelephonyDtmfProvider
{
    /// <summary>
    /// Sends DTMF digits to an active call.
    /// </summary>
    /// <param name="request">The request describing the call and the digits to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default);
}
