using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the hierarchy activity log.
/// </summary>
public class HierarchyActivityViewModel
{
    /// <summary>
    /// Gets or sets the events on the current page.
    /// </summary>
    public List<HierarchyAuditEvent> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the child tenant the log is filtered to, or <see langword="null"/>.
    /// </summary>
    public ChildTenantEntry Child { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    public HierarchyLabels Labels { get; set; }
}
