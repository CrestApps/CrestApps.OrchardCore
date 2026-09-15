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

    /// <summary>
    /// Revokes a single browser credential owned by the specified user, deleting it at Telnyx. Used by a
    /// renewing soft phone to tear down the exact credential it just superseded, so renewals do not accumulate
    /// live credentials. A credential that is not found, not owned by the user, or already revoked is ignored.
    /// </summary>
    /// <param name="userId">The authenticated user the credential must belong to.</param>
    /// <param name="credentialId">The provider credential identifier to revoke.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a live credential was revoked; otherwise <see langword="false"/>.</returns>
    Task<bool> RevokeCredentialAsync(string userId, string credentialId, string reason, CancellationToken cancellationToken = default);
}
