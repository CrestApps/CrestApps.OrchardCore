using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// A row of the child tenants list: a child tenant, with what the current user may do with it.
/// </summary>
public sealed class ChildTenantListItem
{
    /// <summary>
    /// Gets or sets the child tenant.
    /// </summary>
    public ChildTenantInfo Info { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    public HierarchyLabels Labels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may open the child tenant without signing in again.
    /// </summary>
    public bool CanEnter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the row can be selected for a bulk action.
    /// </summary>
    public bool CanSelect { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may create child tenants, and so retry or discard a failed setup.
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may edit, suspend, resume and reload the child tenant.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may change the features of the child tenant.
    /// </summary>
    public bool CanManageFeatures { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may remove the child tenant.
    /// </summary>
    public bool CanRemove { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may manage the access grants of the child tenant.
    /// </summary>
    public bool CanManageAccess { get; set; }

    /// <summary>
    /// Gets or sets the address the list polls while the child tenant is being set up or removed, or
    /// <see langword="null"/> when it is not busy.
    /// </summary>
    public string StatusUrl { get; set; }

    /// <summary>
    /// Gets a value indicating whether the child tenant is being set up or removed.
    /// </summary>
    public bool IsBusy => Info?.Entry?.Status is ChildTenantStatus.Provisioning or ChildTenantStatus.Removing
        || Info?.State == ChildTenantRuntimeState.Initializing;
}
