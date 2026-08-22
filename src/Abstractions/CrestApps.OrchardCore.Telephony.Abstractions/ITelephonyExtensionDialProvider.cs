using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes internal extension operations a telephony provider supports: placing a call to an on-platform user
/// by extension, and adding an on-platform user into an active call as a conference participant. A provider
/// implements this contract only when it can connect two of its own registered endpoints (for example two
/// browser soft phones) without routing through the PSTN.
/// </summary>
public interface ITelephonyExtensionDialProvider
{
    /// <summary>
    /// Places a call to an internal extension. The target user has already been resolved onto the request; the
    /// provider maps that user to its own live endpoint and connects the caller to it.
    /// </summary>
    /// <param name="request">The extension dial request describing the resolved target and caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the placed call or the failure reason.</returns>
    Task<TelephonyResult> DialExtensionAsync(ExtensionDialRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an internal extension into an active call as a conference participant. The provider rings the
    /// resolved target user and joins their leg to the existing conversation.
    /// </summary>
    /// <param name="request">The extension conference request describing the active call and resolved target.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> AddExtensionToConferenceAsync(ExtensionConferenceRequest request, CancellationToken cancellationToken = default);
}
