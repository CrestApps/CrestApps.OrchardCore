using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// A row of the access rules list: a grant, and where it is listed.
/// </summary>
public sealed class AccessGrantListItem
{
    /// <summary>
    /// Gets or sets the grant.
    /// </summary>
    public AccessGrant Grant { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the grant covers every child tenant and is listed on the screen of one
    /// child tenant, where it is read-only.
    /// </summary>
    public bool Inherited { get; set; }

    /// <summary>
    /// Gets or sets the registry entry identifier of the child tenant whose rules are listed, or <see langword="null"/>
    /// on the screen of the rules for every child tenant.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the child tenant whose rules are listed, or <see langword="null"/>.
    /// </summary>
    public string ChildDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    public HierarchyLabels Labels { get; set; }
}
