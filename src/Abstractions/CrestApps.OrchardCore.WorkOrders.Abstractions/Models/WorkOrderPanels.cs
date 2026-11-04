namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// The panels other modules add to a work order's page, such as its visits. A module adds one with a display driver
/// for this model, placed in the Content zone.
/// </summary>
public sealed class WorkOrderPanels
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; set; }

    /// <summary>
    /// Gets or sets whether the user can change the work order.
    /// </summary>
    public bool CanManage { get; set; }
}
