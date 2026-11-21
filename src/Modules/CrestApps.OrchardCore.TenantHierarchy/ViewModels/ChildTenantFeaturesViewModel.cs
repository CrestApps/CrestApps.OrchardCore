using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the features screen of a child tenant.
/// </summary>
public class ChildTenantFeaturesViewModel
{
    /// <summary>
    /// Gets or sets the child tenant.
    /// </summary>
    public ChildTenantInfo Info { get; set; }

    /// <summary>
    /// Gets or sets the list of the features, category by category.
    /// </summary>
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the number of enabled features.
    /// </summary>
    public int EnabledCount { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    public HierarchyLabels Labels { get; set; }
}
