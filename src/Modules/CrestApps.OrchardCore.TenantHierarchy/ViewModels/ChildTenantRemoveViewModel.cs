using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the screen that confirms the removal of a child tenant.
/// </summary>
public class ChildTenantRemoveViewModel
{
    /// <summary>
    /// Gets or sets the name the user typed to confirm the removal.
    /// </summary>
    public string ConfirmName { get; set; }

    /// <summary>
    /// Gets or sets the child tenant.
    /// </summary>
    [BindNever]
    public ChildTenantInfo Info { get; set; }

    /// <summary>
    /// Gets or sets the number of days the child tenant is kept before it is removed.
    /// </summary>
    [BindNever]
    public int GraceDays { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    [BindNever]
    public HierarchyLabels Labels { get; set; }
}
