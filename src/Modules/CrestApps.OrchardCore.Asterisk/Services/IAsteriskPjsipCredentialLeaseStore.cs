using CrestApps.OrchardCore.Asterisk.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Durable, per-tenant store for browser SIP credential leases. Because every query runs through the
/// tenant's own YesSql session, results are inherently scoped to the current tenant; there is no
/// prefix scan over any shared PJSIP Realtime table and no path that can observe another tenant's leases.
/// This store is the authority for credential ownership, expiry, cap enforcement, revocation, and cleanup.
/// </summary>
internal interface IAsteriskPjsipCredentialLeaseStore
{
    /// <summary>
    /// Persists a newly issued credential lease.
    /// </summary>
    /// <param name="lease">The lease to persist.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the lease has been saved.</returns>
    Task CreateAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes made to an existing credential lease.
    /// </summary>
    /// <param name="lease">The lease to update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the lease has been saved.</returns>
    Task UpdateAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a credential lease.
    /// </summary>
    /// <param name="lease">The lease to delete.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the lease has been deleted.</returns>
    Task DeleteAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the credential lease for the supplied authorization user, when one exists for the current tenant.
    /// </summary>
    /// <param name="authorizationUser">The authorization user identifying the lease.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching lease, or <see langword="null"/> when none exists.</returns>
    Task<AsteriskPjsipCredentialLease> GetByAuthorizationUserAsync(
        string authorizationUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the live (non-revoked, non-expired) leases owned by the supplied user.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="nowUtc">The current UTC time used to exclude expired leases.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user's live leases.</returns>
    Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveByUserAsync(
        string userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every non-deleted lease owned by the supplied user, including expired leases whose realtime
    /// rows may not yet have been swept.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user's leases.</returns>
    Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the live (non-revoked, non-expired) leases bound to the supplied media session.
    /// </summary>
    /// <param name="sessionId">The server-owned media session identifier.</param>
    /// <param name="nowUtc">The current UTC time used to exclude expired leases.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The live leases bound to the session.</returns>
    Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveBySessionAsync(
        string sessionId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a bounded batch of leases for the current tenant that are expired or revoked and therefore
    /// eligible for cleanup.
    /// </summary>
    /// <param name="nowUtc">The current UTC time used to detect expired leases.</param>
    /// <param name="maxCount">The maximum number of leases to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The expired or revoked leases to reclaim.</returns>
    Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListExpiredOrRevokedAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default);
}
