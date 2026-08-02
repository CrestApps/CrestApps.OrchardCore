using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Provides a high-level facade the soft phone uses to control calls. Implementations resolve the
/// configured default <see cref="ITelephonyProvider"/> and delegate the requested operation to it.
/// </summary>
public interface ITelephonyService
{
    /// <summary>
    /// Places an outbound call using the default provider.
    /// </summary>
    /// <param name="request">The dial request describing the destination and caller identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the placed call or the failure reason.</returns>
    Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends an active call using the default provider.
    /// </summary>
    /// <param name="call">A reference to the call to end.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places an active call on hold using the default provider.
    /// </summary>
    /// <param name="call">A reference to the call to place on hold.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a call that is currently on hold using the default provider.
    /// </summary>
    /// <param name="call">A reference to the call to resume.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mutes the local audio of an active call using the default provider.
    /// </summary>
    /// <param name="call">A reference to the call to mute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unmutes the local audio of an active call using the default provider.
    /// </summary>
    /// <param name="call">A reference to the call to unmute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers an active call to another destination using the default provider.
    /// </summary>
    /// <param name="request">The transfer request describing the destination and transfer mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges two active calls into a single conference using the default provider.
    /// </summary>
    /// <param name="request">The merge request describing the calls to join.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends DTMF digits to an active call using the default provider.
    /// </summary>
    /// <param name="request">The request describing the call and the digits to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers a ringing inbound call using the default provider.
    /// </summary>
    /// <param name="call">A reference to the inbound call to answer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a ringing inbound call using the default provider.
    /// </summary>
    /// <param name="call">A reference to the inbound call to reject.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a ringing inbound call to voicemail using the default provider.
    /// </summary>
    /// <param name="call">A reference to the inbound call to send to voicemail.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the bootstrap configuration a soft phone client needs to connect to the default provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="TelephonyClientCredentials"/> for the default provider.</returns>
    Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transfer destinations from the configured provider directory.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider directory lookup result.</returns>
    Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities of the configured default provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The capabilities of the default provider, or <see cref="TelephonyCapabilities.None"/> when no provider is configured.</returns>
    Task<TelephonyCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
}
