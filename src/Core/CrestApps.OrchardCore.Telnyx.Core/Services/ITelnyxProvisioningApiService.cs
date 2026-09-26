namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Uses a Telnyx API key to auto-provision (find-or-create) the resources the integration needs: a Call
/// Control application (with the webhook URL), a Credential SIP connection, and an outbound voice profile,
/// and to discover numbers for the caller id. A Telnyx API key carries full account access, so no OAuth is
/// required.
/// </summary>
public interface ITelnyxProvisioningApiService
{
    /// <summary>
    /// Provisions (idempotently, by name) the Telnyx resources the integration needs and returns the
    /// resolved ids.
    /// </summary>
    /// <param name="apiKey">The Telnyx API key.</param>
    /// <param name="apiBaseUrl">The Telnyx REST API base address (trailing slash terminated).</param>
    /// <param name="webhookUrl">The tenant webhook URL to bind to the Call Control application.</param>
    /// <param name="resourceName">The stable name used to find-or-create the resources.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provisioning result.</returns>
    Task<TelnyxProvisioningResult> ConnectAsync(
        string apiKey,
        string apiBaseUrl,
        string webhookUrl,
        string resourceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the provisioned resources at Telnyx (ignoring resources already gone).
    /// </summary>
    /// <param name="apiKey">The Telnyx API key.</param>
    /// <param name="apiBaseUrl">The Telnyx REST API base address.</param>
    /// <param name="connectionId">The Call Control application id to delete.</param>
    /// <param name="sipConnectionId">The Credential connection id to delete.</param>
    /// <param name="outboundVoiceProfileId">The outbound voice profile id to delete.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the resources were deleted or already absent.</returns>
    Task<bool> DisconnectAsync(
        string apiKey,
        string apiBaseUrl,
        string connectionId,
        string sipConnectionId,
        string outboundVoiceProfileId,
        CancellationToken cancellationToken = default);
}
