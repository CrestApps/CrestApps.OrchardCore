using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Gives a new work order an unscheduled visit, and cancels the open visits of a canceled one.
/// </summary>
public sealed class DispatchWorkOrderObserver : IWorkOrderObserver
{
    // Lazy: the dispatch service moves work orders, so it needs the work order service, which calls this observer.
    private readonly Lazy<IDispatchService> _dispatch;
    private readonly IDispatchSettingsProvider _settingsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchWorkOrderObserver"/> class.
    /// </summary>
    public DispatchWorkOrderObserver(Lazy<IDispatchService> dispatch, IDispatchSettingsProvider settingsProvider)
    {
        _dispatch = dispatch;
        _settingsProvider = settingsProvider;
    }

    /// <inheritdoc/>
    public async Task OnEventAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        if (eventName == WorkOrdersConstants.Events.Created)
        {
            if ((await _settingsProvider.GetAsync()).CreateVisitsForNewWorkOrders)
            {
                await _dispatch.Value.CreateAsync(workOrder, null, cancellationToken);
            }

            return;
        }

        if (eventName == WorkOrdersConstants.Events.StatusChanged && workOrder.Status == WorkOrderStatus.Canceled)
        {
            foreach (var visit in await _dispatch.Value.ListForWorkOrderAsync(workOrder.ItemId, cancellationToken))
            {
                if (Core.VisitStatusRules.CanMove(visit.Status, VisitStatus.Canceled))
                {
                    await _dispatch.Value.ChangeStatusAsync(visit.ItemId, VisitStatus.Canceled, workOrder.CancelReason ?? "The work order was canceled.", null, cancellationToken);
                }
            }
        }
    }
}

/// <summary>
/// Keeps what open visits show of their work order up to date when it changes.
/// </summary>
public sealed class DispatchWorkOrderHandler : IWorkOrderHandler
{
    private readonly ServiceVisitStore _store;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchWorkOrderHandler"/> class.
    /// </summary>
    public DispatchWorkOrderHandler(ServiceVisitStore store, ISession session)
    {
        _store = store;
        _session = session;
    }

    /// <inheritdoc/>
    public Task CreatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task UpdatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        var visits = await _session.Query<ServiceVisit, Indexes.ServiceVisitIndex>(index => index.WorkOrderId == workOrder.ItemId && index.IsOpen, collection: DispatchConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var visit in visits)
        {
            DefaultDispatchService.CopyFrom(visit, workOrder);
            await _store.UpdateAsync(visit, cancellationToken);
        }
    }
}
