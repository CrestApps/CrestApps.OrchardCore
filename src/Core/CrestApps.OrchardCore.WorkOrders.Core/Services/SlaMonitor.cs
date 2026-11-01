using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Raises the service level warning and breach of each target once, for open work orders whose clock runs.
/// </summary>
public sealed class SlaMonitor
{
    private readonly ISession _session;
    private readonly WorkOrderStore _store;
    private readonly WorkOrderSettingManager<ServiceLevelPolicy> _policies;
    private readonly IEnumerable<IWorkOrderObserver> _observers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlaMonitor"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="store">The work order store.</param>
    /// <param name="policies">The service level policies.</param>
    /// <param name="observers">The observers.</param>
    /// <param name="clock">The clock.</param>
    public SlaMonitor(ISession session, WorkOrderStore store, WorkOrderSettingManager<ServiceLevelPolicy> policies, IEnumerable<IWorkOrderObserver> observers, IClock clock)
    {
        _session = session;
        _store = store;
        _policies = policies;
        _observers = observers;
        _clock = clock;
    }

    /// <summary>
    /// Looks at the open work orders with targets.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many warnings and breaches were raised.</returns>
    public async Task<int> CheckAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var open = await _session.Query<WorkOrder, WorkOrderIndex>(index => index.IsOpen && (index.RespondByUtc != null || index.ResolveByUtc != null), collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
        var policies = (await _policies.GetAllAsync(cancellationToken)).ToDictionary(policy => policy.ItemId, StringComparer.Ordinal);
        var raised = 0;

        foreach (var workOrder in open)
        {
            if (workOrder.Sla.PausedSinceUtc is not null)
            {
                continue;
            }

            var warningPercent = policies.TryGetValue(workOrder.Sla.PolicyId ?? string.Empty, out var policy) ? policy.WarningPercent : 80;
            var events = new List<(string Name, string Target)>();

            if (workOrder.Sla.FirstResponseUtc is null && workOrder.Sla.RespondByUtc is { } respondBy)
            {
                (workOrder.Sla.ResponseWarned, workOrder.Sla.ResponseBreached) = Evaluate(workOrder.CreatedUtc, respondBy, warningPercent, now, workOrder.Sla.ResponseWarned, workOrder.Sla.ResponseBreached, "Response", events);
            }

            if (workOrder.Sla.ResolveByUtc is { } resolveBy)
            {
                (workOrder.Sla.ResolutionWarned, workOrder.Sla.ResolutionBreached) = Evaluate(workOrder.CreatedUtc, resolveBy, warningPercent, now, workOrder.Sla.ResolutionWarned, workOrder.Sla.ResolutionBreached, "Resolution", events);
            }

            if (events.Count == 0)
            {
                continue;
            }

            await _store.UpdateAsync(workOrder, cancellationToken);

            foreach (var (name, target) in events)
            {
                raised++;

                foreach (var observer in _observers)
                {
                    await observer.OnEventAsync(workOrder, name, new Dictionary<string, object> { ["Target"] = target }, cancellationToken);
                }
            }
        }

        return raised;
    }

    private static (bool Warned, bool Breached) Evaluate(DateTime startUtc, DateTime dueUtc, int warningPercent, DateTime now, bool warned, bool breached, string target, List<(string Name, string Target)> events)
    {
        if (now >= dueUtc && !breached)
        {
            events.Add((WorkOrdersConstants.Events.SlaBreached, target));

            return (true, true);
        }

        if (!warned && !breached && now >= SlaClock.WarningAt(startUtc, dueUtc, warningPercent))
        {
            events.Add((WorkOrdersConstants.Events.SlaWarning, target));

            return (true, false);
        }

        return (warned, breached);
    }
}
