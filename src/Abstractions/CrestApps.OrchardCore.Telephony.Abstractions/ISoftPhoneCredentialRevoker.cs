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
}
