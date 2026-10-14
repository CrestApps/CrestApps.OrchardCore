namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Represents the session of a parent user inside a child tenant. The child validates it with the parent at a
/// regular interval. Only the hash of the session identifier is stored.
/// </summary>
public sealed class DelegatedAccessSession
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the session identifier.
    /// </summary>
    public string SessionHash { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier of the child tenant.
    /// </summary>
    public string ChildTenantId { get; set; }

    /// <summary>
    /// Gets or sets the parent user.
    /// </summary>
    public string ParentUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent user name.
    /// </summary>
    public string ParentUserName { get; set; }

    /// <summary>
    /// Gets or sets the parent sign-in that started the session.
    /// </summary>
    public string ParentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the security stamp of the parent user when the session started.
    /// </summary>
    public string SecurityStamp { get; set; }

    /// <summary>
    /// Gets or sets the authentication methods of the parent sign-in.
    /// </summary>
    public string[] AuthenticationMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets the IP address of the browser that started the session.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets when the session started.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the child last validated the session.
    /// </summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>
    /// Gets or sets when the session was ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets why the session was ended.
    /// </summary>
    public string EndReason { get; set; }
}
