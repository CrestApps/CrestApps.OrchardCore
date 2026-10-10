using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the child tenants list.
/// </summary>
public class ChildTenantsIndexViewModel
{
    /// <summary>
    /// Gets or sets the child tenants on the current page.
    /// </summary>
    public List<ChildTenantInfo> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the search, filter and sort.
    /// </summary>
    public ChildTenantListOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets the number of child tenants that match the filter.
    /// </summary>
    public int FilteredCount { get; set; }

    /// <summary>
    /// Gets or sets the number of child tenants the parent owns.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the number of child tenants the policy allows.
    /// </summary>
    public int MaxChildren { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    public HierarchyLabels Labels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host-level guards are installed.
    /// </summary>
    public bool HostGuardInstalled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may create child tenants.
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may edit, suspend, resume and reload child tenants.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may change the features of child tenants.
    /// </summary>
    public bool CanManageFeatures { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may remove child tenants.
    /// </summary>
    public bool CanRemove { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may manage access grants.
    /// </summary>
    public bool CanManageAccess { get; set; }

    /// <summary>
    /// Gets or sets the registry entries the user may enter.
    /// </summary>
    public HashSet<string> EnterableEntryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the state filter options.
    /// </summary>
    public List<SelectListItem> StateOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the sort options.
    /// </summary>
    public List<SelectListItem> SortOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the bulk actions.
    /// </summary>
    public List<SelectListItem> BulkActions { get; set; } = [];
}
