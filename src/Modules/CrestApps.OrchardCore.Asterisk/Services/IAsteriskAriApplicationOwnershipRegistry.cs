namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Coordinates process-wide ownership of Asterisk ARI (BaseUrl, ApplicationName) pairs across all
/// active tenant containers on this node to prevent cross-tenant Stasis event delivery. Ownership is
/// reference-counted by a per-shell-generation token so that two overlapping generations of the same
/// tenant (during an Orchard shell reload) can both hold the pair without a release from the retiring
/// generation freeing it while the activating generation is still running.
/// </summary>
internal interface IAsteriskAriApplicationOwnershipRegistry
{
    /// <summary>
    /// Atomically claims the normalized (<paramref name="baseUrl"/>, <paramref name="applicationName"/>)
    /// pair for <paramref name="tenantName"/> under <paramref name="ownershipToken"/>. Returns
    /// <see langword="true"/> if the tenant now owns the pair (newly claimed, already the owner, or a
    /// concurrent generation of the same tenant owns it); returns <see langword="false"/> if a different
    /// tenant already owns it. When <paramref name="baseUrl"/>, <paramref name="applicationName"/>, or
    /// <paramref name="ownershipToken"/> is <see langword="null"/> or whitespace the method returns
    /// <see langword="true"/> because an unconfigured tenant starts no listener and holds nothing.
    /// </summary>
    /// <param name="baseUrl">The ARI base URL of the Asterisk server.</param>
    /// <param name="applicationName">The Stasis application name.</param>
    /// <param name="tenantName">The name of the tenant claiming ownership.</param>
    /// <param name="ownershipToken">The per-shell-generation token that reference-counts this claim.</param>
    bool TryClaim(string baseUrl, string applicationName, string tenantName, string ownershipToken);

    /// <summary>
    /// Releases the claim held under <paramref name="ownershipToken"/> for every pair it references.
    /// A pair's ownership is only removed when its last outstanding token is released, so a retiring
    /// shell generation releasing its token never frees a pair still held by a newer generation of the
    /// same tenant. Called when a tenant is terminating so other tenants may reclaim the pair after a
    /// reconfiguration.
    /// </summary>
    /// <param name="ownershipToken">The per-shell-generation token whose claims are being released.</param>
    void Release(string ownershipToken);

    /// <summary>
    /// Returns <see langword="true"/> when the normalized (<paramref name="baseUrl"/>,
    /// <paramref name="applicationName"/>) pair is currently owned by a tenant other than
    /// <paramref name="tenantName"/>. This is a read-only observation that never mutates ownership and is
    /// intended for advisory availability checks; authoritative enforcement happens through
    /// <see cref="TryClaim"/> on every ARI operation path. When <paramref name="baseUrl"/> or
    /// <paramref name="applicationName"/> is <see langword="null"/> or whitespace the method returns
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="baseUrl">The ARI base URL of the Asterisk server.</param>
    /// <param name="applicationName">The Stasis application name.</param>
    /// <param name="tenantName">The name of the tenant asking whether another tenant owns the pair.</param>
    bool IsOwnedByAnotherTenant(string baseUrl, string applicationName, string tenantName);
}
