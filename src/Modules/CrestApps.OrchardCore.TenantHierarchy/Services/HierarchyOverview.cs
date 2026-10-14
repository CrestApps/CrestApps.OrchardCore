using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Describes the whole tenant hierarchy, as the Default tenant sees it.
/// </summary>
public sealed class HierarchyOverview
{
    /// <summary>
    /// Gets or sets the parent tenants with their child tenants.
    /// </summary>
    public List<HierarchyTreeNode> Parents { get; set; } = [];

    /// <summary>
    /// Gets or sets the child tenants whose parent no longer exists.
    /// </summary>
    public List<HierarchyTreeNode> Orphans { get; set; } = [];

    /// <summary>
    /// Gets or sets the tenants that are not part of a hierarchy and can be made into parents.
    /// </summary>
    public List<ShellSettings> Candidates { get; set; } = [];
}
