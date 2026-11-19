namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// The Work Orders settings of a shared service level policy, kept in the policy's <c>Properties</c>: the statuses its
/// timers stop in.
/// </summary>
public sealed class WorkOrderServiceLevelSettings
{
    /// <summary>
    /// Gets or sets the statuses a work order's timers stop in while it waits. A sub-status that pauses service levels
    /// stops them too.
    /// </summary>
    public List<WorkOrderStatus> PauseStatuses { get; set; } = [];
}
