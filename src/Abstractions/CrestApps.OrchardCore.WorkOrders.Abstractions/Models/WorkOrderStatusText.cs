namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// Gives the words people read for a work order status.
/// </summary>
public static class WorkOrderStatusText
{
    /// <summary>
    /// Gives the words for a status, such as "In progress".
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The words.</returns>
    public static string Text(this WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.InProgress => "In progress",
        WorkOrderStatus.OnHold => "On hold",
        _ => status.ToString(),
    };
}
