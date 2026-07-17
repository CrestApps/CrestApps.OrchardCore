namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Durable, per-tenant record of an issued browser SIP credential. Persisted in the tenant's own YesSql
/// store, this lease is the single source of truth for tenant/user/session ownership, expiry, per-user
/// cap enforcement, revocation, and cleanup. The distributed cache is only a read-through performance
/// cache over these leases; expiry is always read from the lease and is never inferred from a cache miss.
/// </summary>
public sealed class AsteriskPjsipCredentialLease
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the authorization user that uniquely identifies the provisioned PJSIP Realtime rows.
    /// </summary>
    public string AuthorizationUser { get; set; }

    /// <summary>
    /// Gets or sets the name of the tenant that owns the credential.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user the credential is bound to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the server-owned media session identifier the credential is bound to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the non-authoritative interaction identifier carried as metadata, when supplied.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential was issued.
    /// </summary>
    public DateTime IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential expires. This durable value is the authority for expiry.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credential was revoked, when it has been revoked.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded when the credential was revoked, when it has been revoked.
    /// </summary>
    public string RevocationReason { get; set; }
}
