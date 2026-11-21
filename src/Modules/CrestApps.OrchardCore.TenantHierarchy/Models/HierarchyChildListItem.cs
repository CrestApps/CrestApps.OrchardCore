using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// A row of a child tenants list of the platform: the child tenants of one parent, or the orphaned child tenants.
/// </summary>
public sealed class HierarchyChildListItem
{
    /// <summary>
    /// Gets or sets the child tenant.
    /// </summary>
    public HierarchyTreeNode Child { get; set; }

    /// <summary>
    /// Gets or sets the tenant name of the parent the child tenant is listed under, or <see langword="null"/> for a
    /// child tenant whose parent no longer exists.
    /// </summary>
    public string ParentTenantName { get; set; }

    /// <summary>
    /// Gets or sets the parents the child tenant can be moved to.
    /// </summary>
    public IReadOnlyList<HierarchyTreeNode> MoveTargets { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the parent of the child tenant no longer exists.
    /// </summary>
    public bool IsOrphan => string.IsNullOrEmpty(ParentTenantName);
}
