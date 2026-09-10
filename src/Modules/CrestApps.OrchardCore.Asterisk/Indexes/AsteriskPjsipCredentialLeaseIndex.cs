using YesSql.Indexes;

namespace CrestApps.OrchardCore.Asterisk.Indexes;

/// <summary>
/// YesSql index used to query durable browser SIP credential leases by authorization user, owning user,
/// media session, expiry, and revocation state.
/// </summary>
public sealed class AsteriskPjsipCredentialLeaseIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the authorization user that uniquely identifies the credential.
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
    /// Gets or sets the UTC time the credential expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the credential has been revoked.
    /// </summary>
    public bool Revoked { get; set; }
}
