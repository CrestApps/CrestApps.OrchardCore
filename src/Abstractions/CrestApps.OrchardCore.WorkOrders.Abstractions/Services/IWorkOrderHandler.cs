using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// Adds to a work order as it is created, such as its territory from the service address.
/// </summary>
public interface IWorkOrderHandler
{
    /// <summary>
    /// Called before a new work order is saved.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    Task CreatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs before an edited work order is saved.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    Task UpdatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
