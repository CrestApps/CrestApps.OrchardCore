using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Telemetry;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Turns the warnings and breaches of a work order's service level timers into the work-order events workflows,
/// notifications and reports already listen to, and keeps the flags on the work order's service level read model.
/// </summary>
public sealed class WorkOrderServiceLevelObserver : IServiceLevelObserver
{
    private readonly WorkOrderStore _store;
    private readonly IEnumerable<IWorkOrderObserver> _observers;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderServiceLevelObserver"/> class.
    /// </summary>
    /// <param name="store">The work order store.</param>
    /// <param name="observers">The work order observers.</param>
    public WorkOrderServiceLevelObserver(WorkOrderStore store, IEnumerable<IWorkOrderObserver> observers)
    {
        _store = store;
        _observers = observers;
    }

    /// <inheritdoc/>
    public Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => RaiseAsync(timer, breached: false, cancellationToken);

    /// <inheritdoc/>
    public Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => RaiseAsync(timer, breached: true, cancellationToken);

    private async Task RaiseAsync(ServiceLevelTimer timer, bool breached, CancellationToken cancellationToken)
    {
        if (!string.Equals(timer?.Subject.Kind, WorkOrdersConstants.RecordKind, StringComparison.Ordinal))
        {
            return;
        }

        var workOrder = await _store.FindByIdAsync(timer.Subject.Id, cancellationToken);

        if (workOrder is null)
        {
            return;
        }

        var target = WorkOrderServiceLevels.TargetName(timer.Metric);

        if (target is null)
        {
            return;
        }

        workOrder.Sla ??= new WorkOrderSla();

        if (target == WorkOrderServiceLevels.ResponseTarget)
        {
            workOrder.Sla.ResponseWarned = true;
            workOrder.Sla.ResponseBreached |= breached;
        }
        else
        {
            workOrder.Sla.ResolutionWarned = true;
            workOrder.Sla.ResolutionBreached |= breached;
        }

        await _store.UpdateAsync(workOrder, cancellationToken);

        var eventName = breached ? WorkOrdersConstants.Events.SlaBreached : WorkOrdersConstants.Events.SlaWarning;

        WorkOrdersTelemetry.RecordSlaEvent(breached, target);

        foreach (var observer in _observers)
        {
            await observer.OnEventAsync(workOrder, eventName, new Dictionary<string, object> { ["Target"] = target }, cancellationToken);
        }
    }
}
