using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Describes one tenant in the hierarchy tree the platform sees.
/// </summary>
public sealed class HierarchyTreeNode
{
    /// <summary>
    /// Gets or sets the shell settings of the tenant.
    /// </summary>
    public ShellSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the display name of the tenant.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the base address of the tenant.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the child tenants, for a parent tenant.
    /// </summary>
    public List<HierarchyTreeNode> Children { get; set; } = [];

    /// <summary>
    /// Gets or sets the problems found with the tenant, for example a registry mismatch or a blocked feature that is on.
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}
