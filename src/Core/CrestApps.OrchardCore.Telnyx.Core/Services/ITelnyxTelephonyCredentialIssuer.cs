namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Mints and revokes short-lived browser SIP credentials from Telnyx so the soft phone can register
/// directly with the Telnyx SIP-over-WebSocket registrar and carry the call audio in the browser.
/// </summary>
public interface ITelnyxTelephonyCredentialIssuer
{
    /// <summary>
    /// Issues a new browser SIP credential for the specified authenticated user and records the durable
    /// mapping used to resolve the agent's endpoint and to revoke the credential later.
    /// </summary>
    /// <param name="userId">The authenticated user identifier the credential is bound to.</param>
    /// <param name="displayName">The display name to present in SIP signaling.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The issued credential, or <see langword="null"/> when issuance fails.</returns>
    Task<TelnyxTelephonyCredential> IssueAsync(string userId, string displayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every live browser credential owned by the specified user, deleting them at Telnyx.
    /// </summary>
    /// <param name="userId">The authenticated user identifier whose credentials must be revoked.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of credentials revoked.</returns>
    Task<int> RevokeForUserAsync(string userId, string reason, CancellationToken cancellationToken = default);
}
