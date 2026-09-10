namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Revokes the browser soft-phone credentials owned by an authenticated user. Providers that mint
/// short-lived, server-owned browser SIP credentials implement this so the credentials can be torn
/// down on sign-out or session termination instead of lingering until natural expiry.
/// </summary>
public interface ISoftPhoneCredentialRevoker
{
    /// <summary>
    /// Gets the technical provider name handled by this revoker.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Revokes every live browser credential owned by the specified authenticated user.
    /// </summary>
    /// <param name="userId">The authenticated user identifier whose credentials must be revoked.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of credentials revoked for the user.</returns>
    Task<int> RevokeForUserAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a single browser credential owned by the specified user. A renewing soft phone calls this to
    /// tear down the exact credential it just superseded, so renewals do not accumulate live credentials.
    /// Providers whose credentials are not addressable individually may leave the default no-op, in which case
    /// the credential is simply left to expire naturally.
    /// </summary>
    /// <param name="userId">The authenticated user the credential must belong to.</param>
    /// <param name="credentialId">The provider credential identifier to revoke.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a live credential was revoked; otherwise <see langword="false"/>.</returns>
    Task<bool> RevokeCredentialAsync(
        string userId,
        string credentialId,
        string reason,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
