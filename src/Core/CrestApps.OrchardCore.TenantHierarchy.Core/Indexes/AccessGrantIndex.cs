using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="AccessGrant"/> documents of a parent tenant.
/// </summary>
public sealed class AccessGrantIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the grant identifier.
    /// </summary>
    public string GrantId { get; set; }

    /// <summary>
    /// Gets or sets who the grant applies to.
    /// </summary>
    public string PrincipalType { get; set; }

    /// <summary>
    /// Gets or sets the parent user identifier or the parent role name.
    /// </summary>
    public string PrincipalId { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant, or <see langword="null"/> for every child tenant.
    /// </summary>
    public string ChildEntryId { get; set; }
}
