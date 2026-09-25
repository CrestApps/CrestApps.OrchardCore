namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Lets a call be sent to voicemail once. Sending a caller to voicemail answers their leg and plays the greeting, so a
/// second request for the same call -- a repeat click, or two paths acting on one click -- would answer and greet the
/// caller again, and they would hear the greeting twice before the beep.
/// </summary>
public interface ITelephonyVoicemailSendGuard
{
    /// <summary>
    /// Claims sending a call to voicemail.
    /// </summary>
    /// <param name="callId">The provider's identifier of the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when this request may send the call to voicemail; <see langword="false"/> when the call
    /// is already being, or has already been, sent to voicemail.
    /// </returns>
    Task<bool> TryClaimAsync(string callId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives up a claim whose attempt did not send the call to voicemail, so a later request may try again.
    /// </summary>
    /// <param name="callId">The provider's identifier of the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the claim is released.</returns>
    Task ReleaseAsync(string callId, CancellationToken cancellationToken = default);
}
