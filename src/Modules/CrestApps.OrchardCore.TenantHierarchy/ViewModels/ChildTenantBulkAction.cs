namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The bulk actions of the child tenants list.
/// </summary>
public enum ChildTenantBulkAction
{
    /// <summary>
    /// No action.
    /// </summary>
    None,

    /// <summary>
    /// Suspend the selected child tenants.
    /// </summary>
    Suspend,

    /// <summary>
    /// Resume the selected child tenants.
    /// </summary>
    Resume,

    /// <summary>
    /// Reload the selected child tenants.
    /// </summary>
    Reload,

    /// <summary>
    /// Remove the selected child tenants that are suspended.
    /// </summary>
    Remove,
}
