using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// The permissions of the Work Orders module.
/// </summary>
public static class WorkOrdersPermissions
{
    /// <summary>
    /// Manage service agreements and their schedules.
    /// </summary>
    public static readonly Permission ManageServiceAgreements = new("ManageServiceAgreements", LocalizationSource.Create("Manage service agreements", typeof(WorkOrdersPermissions)));

    /// <summary>
    /// Allows managing work order types, checklist templates, service level policies and sub-statuses.
    /// </summary>
    public static readonly Permission ManageWorkOrderSettings = new("ManageWorkOrderSettings", LocalizationSource.Create("Manage work order types, checklists and service levels", typeof(WorkOrdersPermissions)));

    /// <summary>
    /// Allows creating and changing work orders.
    /// </summary>
    public static readonly Permission ManageWorkOrders = new("ManageWorkOrders", LocalizationSource.Create("Manage work orders", typeof(WorkOrdersPermissions)), [ManageWorkOrderSettings]);

    /// <summary>
    /// Allows seeing work orders.
    /// </summary>
    public static readonly Permission ViewWorkOrders = new("ViewWorkOrders", LocalizationSource.Create("View work orders", typeof(WorkOrdersPermissions)), [ManageWorkOrders]);
}
