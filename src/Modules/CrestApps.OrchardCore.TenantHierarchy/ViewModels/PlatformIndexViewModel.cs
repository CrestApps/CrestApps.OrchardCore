using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the tenant hierarchy screen of the platform.
/// </summary>
public class PlatformIndexViewModel
{
    /// <summary>
    /// Gets or sets the hierarchy.
    /// </summary>
    public HierarchyOverview Overview { get; set; }

    /// <summary>
    /// Gets or sets the parents that match the search.
    /// </summary>
    public List<HierarchyTreeNode> Parents { get; set; } = [];

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host-level guards are installed.
    /// </summary>
    public bool HostGuardInstalled { get; set; }

    /// <summary>
    /// Gets or sets the platform domain parent hosts are built on.
    /// </summary>
    public string PlatformDomain { get; set; }
}
