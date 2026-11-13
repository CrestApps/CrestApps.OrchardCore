using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.WorkOrders.ViewModels;

/// <summary>
/// The Work Orders card of a service level policy's editor.
/// </summary>
public class WorkOrderServiceLevelPolicyViewModel
{
    /// <summary>
    /// Gets or sets the statuses the timers stop in.
    /// </summary>
    public List<WorkOrderStatus> PauseStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses that can pause the timers.
    /// </summary>
    [BindNever]
    public List<WorkOrderStatus> Statuses { get; set; } = [];
}
