using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the platform screen of one parent tenant.
/// </summary>
public class ParentDetailViewModel
{
    /// <summary>
    /// Gets or sets the parent with its children and their warnings.
    /// </summary>
    public HierarchyTreeNode Parent { get; set; }

    /// <summary>
    /// Gets or sets the list of the child tenants of the parent.
    /// </summary>
    public AdminListContext Children { get; set; }

    /// <summary>
    /// Gets or sets the name the user typed to confirm the removal of the parent.
    /// </summary>
    public string ConfirmName { get; set; }
}
