using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Core;

/// <summary>
/// Which status a work order can move to from which. Pure.
/// </summary>
public static class WorkOrderStatusRules
{
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> _next = new()
    {
        [WorkOrderStatus.New] = [WorkOrderStatus.Ready, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Canceled],
        [WorkOrderStatus.Ready] = [WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Canceled],
        [WorkOrderStatus.Scheduled] = [WorkOrderStatus.Ready, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Canceled],
        [WorkOrderStatus.InProgress] = [WorkOrderStatus.Scheduled, WorkOrderStatus.OnHold, WorkOrderStatus.Completed, WorkOrderStatus.Canceled],
        [WorkOrderStatus.OnHold] = [WorkOrderStatus.Ready, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.Canceled],
        [WorkOrderStatus.Completed] = [WorkOrderStatus.Closed, WorkOrderStatus.InProgress],
        [WorkOrderStatus.Closed] = [],
        [WorkOrderStatus.Canceled] = [],
    };

    /// <summary>
    /// Gives the statuses a work order can move to.
    /// </summary>
    /// <param name="from">Its status.</param>
    /// <returns>The statuses.</returns>
    public static IReadOnlyList<WorkOrderStatus> Next(WorkOrderStatus from) => _next.GetValueOrDefault(from, []);

    /// <summary>
    /// Tells whether a work order can move from one status to another.
    /// </summary>
    /// <param name="from">Its status.</param>
    /// <param name="to">The new status.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    public static bool CanMove(WorkOrderStatus from, WorkOrderStatus to) => Next(from).Contains(to);

    /// <summary>
    /// Tells whether a work order in a status is still open.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> unless it is completed, closed or canceled.</returns>
    public static bool IsOpen(WorkOrderStatus status) => status is not (WorkOrderStatus.Completed or WorkOrderStatus.Closed or WorkOrderStatus.Canceled);

    /// <summary>
    /// Tells whether moving to a status counts as the first response.
    /// </summary>
    /// <param name="status">The new status.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsResponse(WorkOrderStatus status) => status is WorkOrderStatus.Ready or WorkOrderStatus.Scheduled or WorkOrderStatus.InProgress or WorkOrderStatus.Completed;
}
