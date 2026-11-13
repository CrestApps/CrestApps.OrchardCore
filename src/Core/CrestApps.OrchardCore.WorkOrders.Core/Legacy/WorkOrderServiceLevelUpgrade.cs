using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Moves Work Orders onto the shared Service Levels block: each policy Work Orders kept becomes a shared policy with the
/// same id (its weekly hours become a Business Hours schedule), and each open work order gets the timers of its policy.
/// </summary>
public sealed class WorkOrderServiceLevelUpgrade
{
    private const int BatchSize = 100;

    private readonly ISession _session;
    private readonly IServiceLevelPolicyManager _policies;
    private readonly INamedCatalogManager<BusinessHoursSchedule> _schedules;
    private readonly IServiceLevelTracker _tracker;
    private readonly WorkOrderServiceLevels _serviceLevels;
    private readonly WorkOrderStore _workOrders;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderServiceLevelUpgrade"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="policies">The shared policy manager.</param>
    /// <param name="schedules">The Business Hours schedule manager.</param>
    /// <param name="tracker">The service level tracker.</param>
    /// <param name="serviceLevels">The work order service levels.</param>
    /// <param name="workOrders">The work order store.</param>
    public WorkOrderServiceLevelUpgrade(
        ISession session,
        IServiceLevelPolicyManager policies,
        INamedCatalogManager<BusinessHoursSchedule> schedules,
        IServiceLevelTracker tracker,
        WorkOrderServiceLevels serviceLevels,
        WorkOrderStore workOrders)
    {
        _session = session;
        _policies = policies;
        _schedules = schedules;
        _tracker = tracker;
        _serviceLevels = serviceLevels;
        _workOrders = workOrders;
    }

    /// <summary>
    /// Copies every policy Work Orders kept, then gives the open work orders their timers. Running it again does
    /// nothing more: a policy already copied, or a work order that has timers, is left alone.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many policies were copied and how many work orders got timers.</returns>
    public async Task<(int Policies, int WorkOrders)> RunAsync(CancellationToken cancellationToken = default)
    {
        var policies = 0;

        foreach (var legacy in await ListLegacyPoliciesAsync(cancellationToken))
        {
            if (await _policies.FindByIdAsync(legacy.ItemId, cancellationToken) is null)
            {
                await ImportAsync(legacy, cancellationToken);
                policies++;
            }
        }

        var workOrders = await StartOpenWorkOrdersAsync(cancellationToken);

        return (policies, workOrders);
    }

    /// <summary>
    /// Creates or updates the shared policy of a Work Orders policy, as stored or as an older recipe carries it.
    /// </summary>
    /// <param name="legacy">The Work Orders policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shared policy.</returns>
    public async Task<ServiceLevelPolicy> ImportAsync(LegacyServiceLevelPolicy legacy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(legacy);

        var existing = string.IsNullOrEmpty(legacy.ItemId) ? null : await _policies.FindByIdAsync(legacy.ItemId, cancellationToken);
        var policy = existing ?? await _policies.NewAsync(cancellationToken: cancellationToken);

        if (existing is null && !string.IsNullOrEmpty(legacy.ItemId))
        {
            policy.ItemId = legacy.ItemId;
        }

        policy.Name = legacy.Name;
        policy.Scope = WorkOrdersConstants.ServiceLevelScope;
        policy.WarningPercent = legacy.WarningPercent is >= 1 and <= 99 ? legacy.WarningPercent : 80;
        policy.Targets = [.. ToTargets(legacy.Targets)];
        policy.HoursScheduleId = await ResolveScheduleAsync(legacy, cancellationToken);
        policy.Put(new WorkOrderServiceLevelSettings { PauseStatuses = [.. legacy.PauseStatuses ?? []] });

        if (legacy.CreatedUtc != default)
        {
            policy.CreatedUtc = legacy.CreatedUtc;
        }

        if (existing is null)
        {
            await _policies.CreateAsync(policy, cancellationToken);
        }
        else
        {
            await _policies.UpdateAsync(policy, cancellationToken: cancellationToken);
        }

        return policy;
    }

    private static IEnumerable<ServiceLevelTarget> ToTargets(IEnumerable<LegacyServiceLevelTarget> targets)
    {
        foreach (var target in targets ?? [])
        {
            if (target.ResponseMinutes > 0)
            {
                yield return new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Priority = target.Priority.ToString(), Minutes = target.ResponseMinutes };
            }

            if (target.ResolveMinutes > 0)
            {
                yield return new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Priority = target.Priority.ToString(), Minutes = target.ResolveMinutes };
            }
        }
    }

    // The calendar the policy named stays its schedule; its own weekly hours become a Business Hours schedule.
    private async Task<string> ResolveScheduleAsync(LegacyServiceLevelPolicy legacy, CancellationToken cancellationToken)
    {
        if (legacy.WeeklyHours is not { Days.Count: > 0 } weekly || weekly.Closes <= weekly.Opens)
        {
            return string.IsNullOrWhiteSpace(legacy.HoursCalendarId) ? null : legacy.HoursCalendarId;
        }

        var name = $"{legacy.Name} hours";
        var schedule = await _schedules.FindByNameAsync(name, cancellationToken);

        if (schedule is not null)
        {
            return schedule.ItemId;
        }

        var open = (weekly.Opens.Hour * 60) + weekly.Opens.Minute;
        var close = (weekly.Closes.Hour * 60) + weekly.Closes.Minute;

        schedule = await _schedules.NewAsync(cancellationToken: cancellationToken);
        schedule.Name = name;
        schedule.Description = $"The hours of the \"{legacy.Name}\" work order service level policy.";
        schedule.Hours = new HoursSchedule
        {
            TimeZoneId = legacy.TimeZoneId,
            Windows = [.. weekly.Days.Distinct().Select(day => new HoursWindow { Day = day, OpenMinute = open, CloseMinute = close })],
        };

        await _schedules.CreateAsync(schedule, cancellationToken);

        return schedule.ItemId;
    }

    private async Task<IReadOnlyList<LegacyServiceLevelPolicy>> ListLegacyPoliciesAsync(CancellationToken cancellationToken)
    {
        var rows = await _session.QueryIndex<ServiceLevelPolicyIndex>(collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
        var documentIds = rows.Select(row => row.DocumentId).Distinct().ToArray();

        if (documentIds.Length == 0)
        {
            return [];
        }

        var policies = await _session.GetAsync<LegacyServiceLevelPolicy>(documentIds, WorkOrdersConstants.CollectionName, cancellationToken);

        return [.. policies.Where(policy => policy is not null)];
    }

    private async Task<int> StartOpenWorkOrdersAsync(CancellationToken cancellationToken)
    {
        var started = 0;
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Starting timers doesn't close a work order, so paging the open ones in document order is stable.
            var batch = await _session.Query<WorkOrder, WorkOrderIndex>(index => index.IsOpen, collection: WorkOrdersConstants.CollectionName)
                .OrderBy(index => index.ItemId)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ListAsync(cancellationToken);
            var workOrders = batch.ToList();

            foreach (var workOrder in workOrders)
            {
                if (await StartAsync(workOrder, cancellationToken))
                {
                    started++;
                }
            }

            if (workOrders.Count < BatchSize)
            {
                break;
            }

            page++;
        }

        return started;
    }

    // The timers count from when the work order was created, moved on by the time it already stood still, so the due
    // times stay close to the ones it had. A response already given completes its timer then; a work order waiting now
    // is paused; a warning or breach already raised is not raised again.
    private async Task<bool> StartAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var sla = workOrder.Sla;

        if (string.IsNullOrEmpty(sla?.PolicyId)
            || (await _tracker.GetTimersAsync(WorkOrderServiceLevels.SubjectOf(workOrder), cancellationToken: cancellationToken)).Count > 0)
        {
            return false;
        }

        var subject = WorkOrderServiceLevels.SubjectOf(workOrder);
        var startUtc = workOrder.CreatedUtc.AddSeconds(Math.Max(0, sla.PausedSeconds));

        foreach (var metric in new[] { ServiceLevelMetrics.FirstResponse, ServiceLevelMetrics.Resolution })
        {
            var (warned, breached) = metric == ServiceLevelMetrics.FirstResponse
                ? (sla.ResponseWarned, sla.ResponseBreached)
                : (sla.ResolutionWarned, sla.ResolutionBreached);

            await _tracker.StartAsync(new ServiceLevelStart
            {
                Subject = subject,
                Metric = metric,
                PolicyId = sla.PolicyId,
                Priority = workOrder.Priority.ToString(),
                StartUtc = startUtc,
                AlreadyWarned = warned || breached,
                AlreadyBreached = breached,
            }, cancellationToken);
        }

        if (sla.FirstResponseUtc is { } respondedUtc)
        {
            await _tracker.CompleteAsync(subject, ServiceLevelMetrics.FirstResponse, respondedUtc, cancellationToken);
        }

        if (sla.PausedSinceUtc is not null)
        {
            await _tracker.PauseAsync(subject, cancellationToken: cancellationToken);
        }

        await _serviceLevels.RefreshAsync(workOrder, cancellationToken);
        await _workOrders.UpdateAsync(workOrder, cancellationToken);

        return true;
    }
}
