namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Represents a one-time code the parent issues for a child tenant. Only the hash of the code is stored.
/// </summary>
public sealed class DelegatedAccessCode
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the code.
    /// </summary>
    public string CodeHash { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant the code is bound to.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier of the child tenant the code is bound to.
    /// </summary>
    public string ChildTenantId { get; set; }

    /// <summary>
    /// Gets or sets the parent user the code is bound to.
    /// </summary>
    public string ParentUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent sign-in that issued the code.
    /// </summary>
    public string ParentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the PKCE code challenge.
    /// </summary>
    public string CodeChallenge { get; set; }

    /// <summary>
    /// Gets or sets the authentication methods of the parent sign-in.
    /// </summary>
    public string[] AuthenticationMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets the IP address of the browser that asked for the code.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets when the code was issued.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the code expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the code was redeemed.
    /// </summary>
    public bool Redeemed { get; set; }
}
