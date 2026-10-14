namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Says that a parent user, or every parent user in a parent role, may enter one child tenant, or every child tenant,
/// with the listed child roles.
/// </summary>
public sealed class AccessGrant
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the random identifier of the grant.
    /// </summary>
    public string GrantId { get; set; }

    /// <summary>
    /// Gets or sets who the grant applies to.
    /// </summary>
    public AccessGrantPrincipalType PrincipalType { get; set; }

    /// <summary>
    /// Gets or sets the parent user identifier, or the parent role name.
    /// </summary>
    public string PrincipalId { get; set; }

    /// <summary>
    /// Gets or sets the parent user name, or the parent role name, for display.
    /// </summary>
    public string PrincipalName { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant. <see langword="null"/> means every child tenant.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets the roles the parent user gets in the child tenant.
    /// </summary>
    public string[] ChildRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets when the grant was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the parent user who created the grant.
    /// </summary>
    public string CreatedByName { get; set; }
}
