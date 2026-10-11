using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

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
    /// Gets or sets the other parents a child can be moved to.
    /// </summary>
    public List<SelectListItem> OtherParents { get; set; } = [];

    /// <summary>
    /// Gets or sets the name the user typed to confirm the removal of the parent.
    /// </summary>
    public string ConfirmName { get; set; }
}
