using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// A row of the parent tenants list of the platform: a parent tenant with its child tenants.
/// </summary>
public sealed class HierarchyParentListItem
{
    /// <summary>
    /// The number of child tenants the row names; the parent's own screen lists the rest.
    /// </summary>
    public const int ChildrenShown = 10;

    /// <summary>
    /// Gets or sets the parent tenant, with its child tenants and the problems found.
    /// </summary>
    public HierarchyTreeNode Parent { get; set; }
}
