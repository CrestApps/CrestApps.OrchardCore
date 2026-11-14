using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Moves Work Orders onto the shared Service Levels block: each policy Work Orders kept becomes a shared policy with the
/// same id (its weekly hours become a Business Hours schedule), and each open work order gets the timers of its policy.
/// </summary>
internal sealed class WorkOrderServiceLevelUpgrade
{
    // The name the policies were stored under before the Work Orders model became the legacy one.
    internal const string StoredPolicyTypeName = "CrestApps.OrchardCore.WorkOrders.Models.ServiceLevelPolicy, CrestApps.OrchardCore.WorkOrders.Abstractions";

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
    /// Gets or sets how many work orders are handled, and committed, at a time.
    /// </summary>
    internal int BatchSize { get; set; } = 100;

    /// <summary>
    /// Copies every policy Work Orders kept, then gives the open work orders their timers. Running it again does
    /// nothing more: a policy already copied, or a work order that has timers, is left alone.
    /// </summary>
    /// <remarks>
    /// The timers read the schedules the policies were given, which other scopes see only once the copy commits; the
    /// migration copies the policies and gives the work orders their timers in a step that runs after it commits.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many policies were copied and how many work orders got timers.</returns>
    public async Task<(int Policies, int WorkOrders)> RunAsync(CancellationToken cancellationToken = default)
        => (await CopyPoliciesAsync(cancellationToken), await StartOpenWorkOrdersAsync(cancellationToken));

    /// <summary>
    /// Copies the policies Work Orders kept to the shared store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were copied.</returns>
    public async Task<int> CopyPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = 0;

        // The schedules created in this run can't be read back until it commits, so their names are kept here.
        var scheduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var legacy in await ListLegacyPoliciesAsync(cancellationToken))
        {
            if (await _policies.FindByIdAsync(legacy.ItemId, cancellationToken) is null)
            {
                await ImportAsync(legacy, scheduleNames, validate: false, cancellationToken);
                policies++;
            }
        }

        return policies;
    }

    /// <summary>
    /// Creates or updates the shared policy of a Work Orders policy an older recipe carries, with the same checks as the
    /// admin. A refused policy is not saved.
    /// </summary>
    /// <param name="legacy">The Work Orders policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shared policy, or the reasons it was refused.</returns>
    public async Task<(ServiceLevelPolicy Policy, IReadOnlyList<string> Errors)> ImportAsync(LegacyServiceLevelPolicy legacy, CancellationToken cancellationToken = default)
    {
        var (policy, errors) = await ImportAsync(legacy, new HashSet<string>(StringComparer.OrdinalIgnoreCase), validate: true, cancellationToken);

        return (policy, errors);
    }

    // The migration copies what the store already holds; a recipe's policy is checked first. The check runs before a
    // schedule is made from the weekly hours, since a schedule created in this scope can't be read back until it commits.
    private async Task<(ServiceLevelPolicy Policy, IReadOnlyList<string> Errors)> ImportAsync(LegacyServiceLevelPolicy legacy, HashSet<string> scheduleNames, bool validate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(legacy);

        var existing = string.IsNullOrEmpty(legacy.ItemId) ? null : await _policies.FindByIdAsync(legacy.ItemId, cancellationToken);
        var policy = existing ?? await _policies.NewAsync(cancellationToken: cancellationToken);

        // A stored policy keeps its id, which its work orders name; a recipe's id is used only when it's a valid id.
        if (existing is null && !string.IsNullOrEmpty(legacy.ItemId) && (validate ? UniqueId.IsValid(legacy.ItemId) : legacy.ItemId.Length <= 26))
        {
            policy.ItemId = legacy.ItemId;
        }

        policy.Name = legacy.Name;
        policy.Scope = WorkOrdersConstants.ServiceLevelScope;
        policy.WarningPercent = legacy.WarningPercent is >= 1 and <= 99 ? legacy.WarningPercent : 80;
        policy.Targets = [.. ToTargets(legacy.Targets)];
        policy.HoursScheduleId = HasWeeklyHours(legacy) ? null : NamedCalendar(legacy);
        policy.Put(new WorkOrderServiceLevelSettings { PauseStatuses = [.. legacy.PauseStatuses ?? []] });

        if (legacy.CreatedUtc != default)
        {
            policy.CreatedUtc = legacy.CreatedUtc;
        }

        if (validate)
        {
            var validation = await _policies.ValidateAsync(policy, cancellationToken);

            if (!validation.Succeeded)
            {
                return (null, [.. validation.Errors.Select(error => error.ErrorMessage)]);
            }
        }

        policy.HoursScheduleId = await ResolveScheduleAsync(legacy, scheduleNames, cancellationToken);

        if (existing is null)
        {
            await _policies.CreateAsync(policy, cancellationToken);
        }
        else
        {
            await _policies.UpdateAsync(policy, cancellationToken: cancellationToken);
        }

        return (policy, []);
    }

    private static bool HasWeeklyHours(LegacyServiceLevelPolicy legacy)
        => legacy.WeeklyHours is { Days.Count: > 0 } weekly && weekly.Closes > weekly.Opens;

    private static string NamedCalendar(LegacyServiceLevelPolicy legacy)
        => string.IsNullOrWhiteSpace(legacy.HoursCalendarId) ? null : legacy.HoursCalendarId;

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

    // The calendar the policy named stays its schedule; its own weekly hours become a new Business Hours schedule named
    // after the policy, with a number added when another schedule already has that name.
    private async Task<string> ResolveScheduleAsync(LegacyServiceLevelPolicy legacy, HashSet<string> scheduleNames, CancellationToken cancellationToken)
    {
        if (!HasWeeklyHours(legacy))
        {
            return NamedCalendar(legacy);
        }

        var weekly = legacy.WeeklyHours;

        var baseName = $"{legacy.Name} hours";
        var name = baseName;

        for (var number = 2; scheduleNames.Contains(name) || await _schedules.FindByNameAsync(name, cancellationToken) is not null; number++)
        {
            name = $"{baseName} ({number})";
        }

        scheduleNames.Add(name);

        var open = (weekly.Opens.Hour * 60) + weekly.Opens.Minute;
        var close = (weekly.Closes.Hour * 60) + weekly.Closes.Minute;

        var schedule = await _schedules.NewAsync(cancellationToken: cancellationToken);
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

        // A tenant on the old version stored the policies under the old class name, which no longer exists: read that
        // name as the legacy model. Only the reading is mapped; the stored rows are left as they are.
        _session.Store.TypeNames[typeof(LegacyServiceLevelPolicy)] = StoredPolicyTypeName;

        var policies = await _session.GetAsync<LegacyServiceLevelPolicy>(documentIds, WorkOrdersConstants.CollectionName, cancellationToken);

        return [.. policies.Where(policy => policy is not null)];
    }

    /// <summary>
    /// Gives the open work orders the timers of their policy, a batch at a time, committing each batch. A work order that
    /// already has timers is left alone, so running it again, or after a run that stopped part way, does nothing to the
    /// work orders already done.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many work orders were given timers.</returns>
    public async Task<int> StartOpenWorkOrdersAsync(CancellationToken cancellationToken = default)
    {
        var started = 0;
        var afterDocumentId = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Paged by document id, which only grows, so a work order created meanwhile never shifts a page.
            var rows = await _session.QueryIndex<WorkOrderIndex>(index => index.IsOpen && index.DocumentId > afterDocumentId, collection: WorkOrdersConstants.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync(cancellationToken);
            var documentIds = rows.Select(row => row.DocumentId).ToArray();

            if (documentIds.Length == 0)
            {
                break;
            }

            afterDocumentId = documentIds[^1];

            foreach (var workOrder in await _session.GetAsync<WorkOrder>(documentIds, WorkOrdersConstants.CollectionName, cancellationToken))
            {
                if (workOrder is not null && await StartAsync(workOrder, cancellationToken))
                {
                    started++;
                }
            }

            await _session.SaveChangesAsync(cancellationToken);

            if (documentIds.Length < BatchSize)
            {
                break;
            }
        }

        return started;
    }

    // The timers count from when the work order was created, moved on by the time it already stood still. The due times
    // the work order had win over today's targets, a response already given completes its timer then, a work order
    // waiting now is paused from when it started waiting, and a warning or breach already raised stays raised. Nothing
    // is announced: the timers bring the state the work order already had.
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
            var isResponse = metric == ServiceLevelMetrics.FirstResponse;
            var (warned, breached) = isResponse
                ? (sla.ResponseWarned, sla.ResponseBreached)
                : (sla.ResolutionWarned, sla.ResolutionBreached);

            await _tracker.StartAsync(new ServiceLevelStart
            {
                Subject = subject,
                Metric = metric,
                PolicyId = sla.PolicyId,
                Priority = workOrder.Priority.ToString(),
                StartUtc = startUtc,
                DueUtc = isResponse ? sla.RespondByUtc : sla.ResolveByUtc,
                CompletedUtc = isResponse ? sla.FirstResponseUtc : null,
                Paused = sla.PausedSinceUtc is not null,
                PausedSinceUtc = sla.PausedSinceUtc,
                AlreadyWarned = warned || breached,
                AlreadyBreached = breached,
            }, cancellationToken);
        }

        await _serviceLevels.RefreshAsync(workOrder, cancellationToken);
        await _workOrders.UpdateAsync(workOrder, cancellationToken);

        return true;
    }

    // Reads the work order index table with the id of each row's document, which the index model doesn't expose, so the
    // open work orders can be paged by it. Its name matches the table; nothing writes through it.
    private sealed class WorkOrderIndex : MapIndex
    {
        public long DocumentId { get; set; }

        public bool IsOpen { get; set; }
    }
}
