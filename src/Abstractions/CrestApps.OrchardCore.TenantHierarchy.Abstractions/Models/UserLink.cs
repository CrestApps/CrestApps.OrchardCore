namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Links a local user of a child tenant to the parent user it belongs to. It lives in its own collection of the child
/// database, so the administrators of the child tenant cannot reach it.
/// </summary>
public sealed class UserLink
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the local user identifier in the child tenant.
    /// </summary>
    public string ChildUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent tenant identifier.
    /// </summary>
    public string ParentTenantId { get; set; }

    /// <summary>
    /// Gets or sets the parent user identifier.
    /// </summary>
    public string ParentUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent user name.
    /// </summary>
    public string ParentUserName { get; set; }

    /// <summary>
    /// Gets or sets the roles the tenant hierarchy added to the local user. A role sync removes only these.
    /// </summary>
    public string[] ManagedRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the link is current. A link stops being current when its local user
    /// was deleted and a new one was created. Old links are kept so earlier user identifiers stay traceable.
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>
    /// Gets or sets when the link was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the parent user last entered the child tenant.
    /// </summary>
    public DateTime? LastEnteredUtc { get; set; }
}
