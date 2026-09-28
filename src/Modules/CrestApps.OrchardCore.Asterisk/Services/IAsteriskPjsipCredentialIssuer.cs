namespace CrestApps.OrchardCore.Asterisk.Services;

internal interface IAsteriskPjsipCredentialIssuer
{
    Task<AsteriskPjsipCredential> IssueAsync(
        AsteriskPjsipCredentialIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<AsteriskPjsipCredential> RotateAsync(
        AsteriskPjsipCredentialIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every live browser credential owned by the specified authenticated user. This is invoked
    /// on soft-phone sign-out or session termination so a user's server-owned media sessions are torn
    /// down instead of lingering until natural expiry.
    /// </summary>
    /// <param name="userId">The authenticated user identifier whose credentials must be revoked.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of credentials revoked for the user.</returns>
    Task<int> RevokeUserAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
