using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the voicemail operations a telephony provider supports.
/// </summary>
public interface ITelephonyVoicemailProvider
{
    /// <summary>
    /// Sends a ringing inbound call to voicemail.
    /// </summary>
    /// <param name="call">A reference to the inbound call to send to voicemail.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default);
}
