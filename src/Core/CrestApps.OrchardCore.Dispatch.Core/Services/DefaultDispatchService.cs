using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSql.Services;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Makes, assigns, moves and moves along visits, under a lock per worker.
/// </summary>
public sealed class DefaultDispatchService : IDispatchService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly ServiceVisitStore _store;
    private readonly ISession _session;
    private readonly DispatchResources _resources;
    private readonly SlotFitter _fitter;
    private readonly VisitCalendarWriter _calendarWriter;
    private readonly IDistributedLock _distributedLock;
    private readonly IDispatchSettingsProvider _settingsProvider;
    private readonly Lazy<WorkOrderService> _workOrders;
    private readonly WorkOrderStore _workOrderStore;
    private readonly IEnumerable<IVisitObserver> _observers;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IEnumerable<IDispatchEquipmentService> _equipment;
    private readonly ICalendarAvailabilityService _availability;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDispatchService"/> class.
    /// </summary>
    public DefaultDispatchService(
        ServiceVisitStore store,
        ISession session,
        DispatchResources resources,
        SlotFitter fitter,
        VisitCalendarWriter calendarWriter,
        IDistributedLock distributedLock,
        IDispatchSettingsProvider settingsProvider,
        Lazy<WorkOrderService> workOrders,
        WorkOrderStore workOrderStore,
        IEnumerable<IVisitObserver> observers,
        WorkOrderSettingManager<WorkOrderType> types,
        IEnumerable<IDispatchEquipmentService> equipment,
        ICalendarAvailabilityService availability,
        IClock clock,
        ILogger<DefaultDispatchService> logger,
        IStringLocalizer<DefaultDispatchService> stringLocalizer)
    {
        _store = store;
        _session = session;
        _resources = resources;
        _fitter = fitter;
        _calendarWriter = calendarWriter;
        _distributedLock = distributedLock;
        _settingsProvider = settingsProvider;
        _workOrders = workOrders;
        _workOrderStore = workOrderStore;
        _observers = observers;
        _types = types;
        _equipment = equipment;
        _availability = availability;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<ServiceVisit> FindAsync(string id, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(id) ? null : await _store.FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceVisit>> ListForWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(workOrderId))
        {
            return [];
        }

        var visits = await _session.Query<ServiceVisit, ServiceVisitIndex>(index => index.WorkOrderId == workOrderId, collection: DispatchConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return visits.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceVisit>> ListScheduledAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string> workerIds = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<ServiceVisit> visits;

        if (workerIds is null)
        {
            var canceled = nameof(VisitStatus.Canceled);

            visits = await _session.Query<ServiceVisit, ServiceVisitIndex>(index => index.StartUtc < toUtc && index.EndUtc > fromUtc && index.Status != canceled, collection: DispatchConstants.CollectionName)
                .ListAsync(cancellationToken);
        }
        else
        {
            var ids = workerIds.ToArray();

            visits = await _session.Query<ServiceVisit, VisitWorkerIndex>(index => index.WorkerId.IsIn(ids) && index.IsActive && index.StartUtc < toUtc && index.EndUtc > fromUtc, collection: DispatchConstants.CollectionName)
                .ListAsync(cancellationToken);
        }

        return visits.DistinctBy(visit => visit.ItemId).OrderBy(visit => visit.StartUtc).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceVisit>> ListUnscheduledAsync(int max = 200, CancellationToken cancellationToken = default)
    {
        var unscheduled = nameof(VisitStatus.Unscheduled);
        var visits = await _session.Query<ServiceVisit, ServiceVisitIndex>(index => index.Status == unscheduled, collection: DispatchConstants.CollectionName)
            .OrderByDescending(index => index.Priority)
            .ThenBy(index => index.DueUtc)
            .ThenBy(index => index.CreatedUtc)
            .Take(max)
            .ListAsync(cancellationToken);

        return visits.ToArray();
    }

    /// <inheritdoc/>
    public async Task<DispatchResult> CreateAsync(WorkOrder workOrder, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        if (!WorkOrderStatusRules.IsOpen(workOrder.Status))
        {
            return DispatchResult.Fail(S["A {0} work order gets no visits.", workOrder.Status.Text().ToLowerInvariant()]);
        }

        var now = _clock.UtcNow;
        var type = string.IsNullOrEmpty(workOrder.TypeId) ? null : await _types.FindByIdAsync(workOrder.TypeId, cancellationToken);
        var visit = new ServiceVisit
        {
            ItemId = CrestApps.Core.UniqueId.GenerateId(),
            CrewSize = Math.Max(1, type?.CrewSize ?? 1),
            RequiredEquipmentTypeIds = [.. type?.RequiredEquipmentTypeIds ?? []],
            Number = await NextNumberAsync(cancellationToken),
            WorkOrderId = workOrder.ItemId,
            Status = VisitStatus.Unscheduled,
            DurationMinutes = workOrder.EstimatedMinutes > 0 ? workOrder.EstimatedMinutes : type?.DefaultDurationMinutes > 0 ? type.DefaultDurationMinutes : 60,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        CopyFrom(visit, workOrder);
        visit.History.Add(new VisitStatusChange { Status = VisitStatus.Unscheduled, Utc = now, By = actor, Note = S["Created."] });

        await _store.CreateAsync(visit, cancellationToken);
        await NotifyAsync(visit, DispatchConstants.Events.Created, null, cancellationToken);

        return new DispatchResult { Visit = visit };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListWorkOrdersWithoutVisitsAsync(CancellationToken cancellationToken = default)
    {
        var open = await _session.QueryIndex<WorkOrders.Core.Indexes.WorkOrderIndex>(index => index.IsOpen, collection: WorkOrders.WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
        var visited = (await _session.QueryIndex<ServiceVisitIndex>(collection: DispatchConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.WorkOrderId)
            .ToHashSet(StringComparer.Ordinal);

        return [.. open.Select(index => index.ItemId).Where(id => !visited.Contains(id)).Distinct(StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public async Task<int> CreateMissingAsync(string actor, CancellationToken cancellationToken = default)
    {
        var made = 0;

        foreach (var workOrderId in await ListWorkOrdersWithoutVisitsAsync(cancellationToken))
        {
            var workOrder = await _workOrderStore.FindByIdAsync(workOrderId, cancellationToken);

            if (workOrder is not null && (await CreateAsync(workOrder, actor, cancellationToken)).Succeeded)
            {
                made++;
            }
        }

        return made;
    }

    /// <inheritdoc/>
    public async Task<DispatchResult> AssignAsync(string visitId, VisitAssignment assignment, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var workerIds = (assignment.WorkerIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

        if (string.IsNullOrEmpty(visitId))
        {
            return DispatchResult.Fail(S["The visit doesn't exist."]);
        }

        if (workerIds.Count == 0)
        {
            return DispatchResult.Fail(S["Choose who does the visit."]);
        }

        if (assignment.DurationMinutes is <= 0 or > 24 * 60)
        {
            return DispatchResult.Fail(S["A visit lasts between a minute and a day."]);
        }

        // Locks first, then every read: the check below must see what the dispatcher who held the lock before saved.
        var lockers = await LockAsync(workerIds.Select(id => DispatchConstants.ResourceLockPrefix + DispatchConstants.ResourceKinds.Worker + "_" + id).Append("DISPATCH_VISIT_" + visitId));

        if (lockers is null)
        {
            return DispatchResult.Fail(S["Someone else is changing these workers' visits. Try again."]);
        }

        try
        {
            var visit = await _store.FindByIdAsync(visitId, cancellationToken);

            if (visit is null)
            {
                return DispatchResult.Fail(S["The visit doesn't exist."]);
            }

            if (!VisitStatusRules.CanAssign(visit.Status))
            {
                return DispatchResult.Fail(S["A visit that is {0} can't be moved.", visit.Status.Text().ToLowerInvariant()]);
            }

            var settings = await _settingsProvider.GetAsync();
            var duration = assignment.DurationMinutes ?? visit.DurationMinutes;
            var start = DateTime.SpecifyKind(assignment.StartUtc, DateTimeKind.Utc);
            var end = start.AddMinutes(duration);
            var here = await _resources.PlaceOfAsync(visit.WorkOrderId, cancellationToken);
            var errors = new List<string>();
            var assigned = new List<WorkerAvailability>();
            SlotFit leadFit = null;

            foreach (var workerId in workerIds)
            {
                var worker = await _resources.FindWorkerAsync(workerId, cancellationToken);

                if (worker is null || worker.Status != Workforce.Models.WorkerStatus.Active)
                {
                    errors.Add(S["The worker doesn't exist or doesn't work here any more."]);

                    continue;
                }

                var availability = await _resources.GetAvailabilityAsync(worker, start.AddHours(-12), end.AddHours(12), visit.ItemId, cancellationToken);
                var (fit, problem) = await _fitter.CheckAsync(availability, here, start, end, settings.BufferMinutes, cancellationToken);

                if (problem is not null)
                {
                    errors.Add(Describe(worker.DisplayName, problem, availability.TimeZoneId));

                    continue;
                }

                leadFit ??= fit;
                assigned.Add(availability);
            }

            if (errors.Count == 0 && workerIds.Count < visit.CrewSize)
            {
                errors.Add(S["This job needs {0} people.", visit.CrewSize]);
            }

            var equipment = errors.Count == 0 ? await FindEquipmentAsync(visit, start, end, errors, cancellationToken) : [];

            if (errors.Count > 0)
            {
                return new DispatchResult { Visit = visit, Errors = errors };
            }

            var previous = visit.Status;
            var now = _clock.UtcNow;
            var moved = visit.StartUtc is not null;
            var window = TimeSpan.FromMinutes(Math.Max(0, settings.ArrivalWindowMinutes));

            visit.WorkerIds = workerIds;
            visit.LeadWorkerId = workerIds[0];
            visit.DurationMinutes = duration;
            visit.StartUtc = start;
            visit.EndUtc = end;
            visit.ArrivalStartUtc = window == TimeSpan.Zero ? start : RoundDown(start - (window / 2), 30);
            visit.ArrivalEndUtc = window == TimeSpan.Zero ? start : visit.ArrivalStartUtc + window;
            visit.TravelMinutes = leadFit?.TravelMinutes;
            visit.TravelKm = leadFit?.TravelKm;
            visit.Pinned = assignment.Pinned ?? visit.Pinned;
            visit.ModifiedUtc = now;

            if (visit.Status == VisitStatus.Unscheduled)
            {
                visit.Status = VisitStatus.Scheduled;
            }

            var names = string.Join(", ", assigned.Select(availability => availability.Worker.DisplayName));
            var lead = assigned[0];

            visit.History.Add(new VisitStatusChange
            {
                Status = visit.Status,
                Utc = now,
                By = actor,
                Note = moved
                    ? S["Moved to {0} at {1}.", names, Format(start, lead.TimeZoneId)]
                    : S["Scheduled for {0} at {1}.", names, Format(start, lead.TimeZoneId)],
            });

            var calendarErrors = (await _calendarWriter.SyncAsync(visit, assigned, Title(visit), actor, cancellationToken))
                .Concat(await _calendarWriter.SyncEquipmentAsync(visit, equipment, Title(visit), actor, cancellationToken));

            foreach (var error in calendarErrors)
            {
                _logger.LogWarning("A calendar refused the job event of visit {VisitNumber}: {Error}", visit.Number, error);
            }

            await _store.UpdateAsync(visit, cancellationToken);
            await MoveWorkOrderAsync(visit.WorkOrderId, WorkOrderStatus.Scheduled, actor, cancellationToken);

            // Saved before the locks are let go, so the next dispatcher's check sees this visit.
            await _session.SaveChangesAsync(cancellationToken);

            await NotifyAsync(visit, DispatchConstants.Events.Assigned, new Dictionary<string, object> { ["PreviousStatus"] = previous.ToString(), ["Moved"] = moved }, cancellationToken);

            return new DispatchResult { Visit = visit };
        }
        finally
        {
            await ReleaseAsync(lockers);
        }
    }

    /// <inheritdoc/>
    public async Task<DispatchResult> UnassignAsync(string visitId, string actor, CancellationToken cancellationToken = default)
    {
        var visit = await FindAsync(visitId, cancellationToken);

        if (visit is null)
        {
            return DispatchResult.Fail(S["The visit doesn't exist."]);
        }

        if (visit.Status is not (VisitStatus.Scheduled or VisitStatus.Dispatched))
        {
            return DispatchResult.Fail(S["Only a scheduled visit that hasn't started can go back to unscheduled."]);
        }

        if (visit.Pinned)
        {
            return DispatchResult.Fail(S["The visit is pinned. Unpin it first."]);
        }

        var previous = visit.Status;
        var now = _clock.UtcNow;

        await _calendarWriter.RemoveAsync(visit, actor, cancellationToken);
        visit.Status = VisitStatus.Unscheduled;
        visit.WorkerIds = [];
        visit.LeadWorkerId = null;
        visit.StartUtc = null;
        visit.EndUtc = null;
        visit.ArrivalStartUtc = null;
        visit.ArrivalEndUtc = null;
        visit.TravelMinutes = null;
        visit.TravelKm = null;
        visit.ModifiedUtc = now;
        visit.History.Add(new VisitStatusChange { Status = VisitStatus.Unscheduled, Utc = now, By = actor, Note = S["Taken off its workers."] });

        await _store.UpdateAsync(visit, cancellationToken);
        await ReleaseWorkOrderAsync(visit, actor, cancellationToken);
        await NotifyAsync(visit, DispatchConstants.Events.Unassigned, new Dictionary<string, object> { ["PreviousStatus"] = previous.ToString() }, cancellationToken);

        return new DispatchResult { Visit = visit };
    }

    /// <inheritdoc/>
    public async Task<DispatchResult> ChangeStatusAsync(string visitId, VisitStatus status, string note, string actor, CancellationToken cancellationToken = default)
    {
        var visit = await FindAsync(visitId, cancellationToken);

        if (visit is null)
        {
            return DispatchResult.Fail(S["The visit doesn't exist."]);
        }

        if (status is VisitStatus.Scheduled or VisitStatus.Unscheduled)
        {
            return DispatchResult.Fail(S["Assign a visit to schedule it, or take it off its workers to unschedule it."]);
        }

        if (status == visit.Status)
        {
            return new DispatchResult { Visit = visit };
        }

        if (!VisitStatusRules.CanMove(visit.Status, status))
        {
            return DispatchResult.Fail(S["A visit can't move from {0} to {1}.", visit.Status.Text(), status.Text()]);
        }

        if (status is VisitStatus.Incomplete or VisitStatus.Canceled && string.IsNullOrWhiteSpace(note))
        {
            return DispatchResult.Fail(status == VisitStatus.Canceled ? S["Say why the visit is canceled."] : S["Say why the visit couldn't be finished."]);
        }

        var previous = visit.Status;
        var now = _clock.UtcNow;

        visit.Status = status;
        visit.ModifiedUtc = now;
        visit.History.Add(new VisitStatusChange { Status = status, Utc = now, By = actor, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() });

        if (status == VisitStatus.Canceled)
        {
            await _calendarWriter.RemoveAsync(visit, actor, cancellationToken);
        }

        if (status == VisitStatus.EnRoute)
        {
            visit.EtaUtc = await EstimateArrivalAsync(visit, now, cancellationToken);
        }

        // The risk is looked at again from the new status.
        visit.AtRiskReason = null;
        visit.AtRiskSinceUtc = null;

        if (status == VisitStatus.Incomplete)
        {
            visit.IncompleteReason = note.Trim();

            var workOrder = await _workOrderStore.FindByIdAsync(visit.WorkOrderId, cancellationToken);

            if (workOrder is not null && WorkOrderStatusRules.IsOpen(workOrder.Status))
            {
                var returnVisit = await CreateAsync(workOrder, actor, cancellationToken);

                visit.ReturnVisitId = returnVisit.Visit?.ItemId;
            }
        }

        await _store.UpdateAsync(visit, cancellationToken);

        if (status is VisitStatus.EnRoute or VisitStatus.OnSite)
        {
            await MoveWorkOrderAsync(visit.WorkOrderId, WorkOrderStatus.InProgress, actor, cancellationToken);
        }
        else if (status == VisitStatus.Canceled)
        {
            await ReleaseWorkOrderAsync(visit, actor, cancellationToken);
        }

        await NotifyAsync(visit, DispatchConstants.Events.StatusChanged, new Dictionary<string, object> { ["PreviousStatus"] = previous.ToString() }, cancellationToken);

        return new DispatchResult { Visit = visit };
    }

    /// <inheritdoc/>
    public async Task<DispatchResult> PinAsync(string visitId, bool pinned, string actor, CancellationToken cancellationToken = default)
    {
        var visit = await FindAsync(visitId, cancellationToken);

        if (visit is null)
        {
            return DispatchResult.Fail(S["The visit doesn't exist."]);
        }

        if (visit.Pinned == pinned)
        {
            return new DispatchResult { Visit = visit };
        }

        visit.Pinned = pinned;
        visit.ModifiedUtc = _clock.UtcNow;
        visit.History.Add(new VisitStatusChange { Status = visit.Status, Utc = visit.ModifiedUtc.Value, By = actor, Note = pinned ? S["Pinned."] : S["Unpinned."] });
        await _store.UpdateAsync(visit, cancellationToken);

        return new DispatchResult { Visit = visit };
    }

    /// <summary>
    /// Copies what lists show from a work order onto a visit.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="workOrder">The work order.</param>
    public static void CopyFrom(ServiceVisit visit, WorkOrder workOrder)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(workOrder);

        visit.WorkOrderNumber = workOrder.Number;
        visit.Summary = workOrder.Summary;
        visit.TypeId = workOrder.TypeId;
        visit.Priority = workOrder.Priority;
        visit.TerritoryId = workOrder.TerritoryId;
        visit.DueUtc = workOrder.Sla?.ResolveByUtc;
    }

    // Picks, for each kind of equipment the job needs, the first one free at the time, keeping what the visit has.
    private async Task<List<DispatchEquipment>> FindEquipmentAsync(ServiceVisit visit, DateTime start, DateTime end, List<string> errors, CancellationToken cancellationToken)
    {
        var picked = new List<DispatchEquipment>();
        var source = _equipment.FirstOrDefault();

        if (source is null || visit.RequiredEquipmentTypeIds.Count == 0)
        {
            return picked;
        }

        var own = visit.CalendarEvents.Where(entry => entry.ResourceKind == DispatchConstants.ResourceKinds.Equipment).Select(entry => entry.EventId).ToList();

        foreach (var typeId in visit.RequiredEquipmentTypeIds.Distinct(StringComparer.Ordinal))
        {
            var choices = (await source.ListAsync(typeId, cancellationToken))
                .Where(item => picked.All(chosen => chosen.AssetId != item.AssetId))
                .OrderByDescending(item => visit.EquipmentAssetIds.Contains(item.AssetId))
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
            DispatchEquipment free = null;

            foreach (var item in choices)
            {
                var busy = await _availability.GetBusyAsync(new CalendarBusyQuery
                {
                    CalendarIds = [item.CalendarId],
                    FromUtc = start,
                    ToUtc = end,
                    ExcludeEventIds = own,
                    IncludeBusyTimeSources = false,
                }, cancellationToken);

                if (busy.Count == 0)
                {
                    free = item;

                    break;
                }
            }

            if (free is null)
            {
                errors.Add(S["No {0} is free then.", await source.TypeNameAsync(typeId, cancellationToken) ?? typeId]);

                continue;
            }

            picked.Add(free);
        }

        return picked;
    }

    // When the lead is expected: now, plus the trip from their last stop today, or from home.
    private async Task<DateTime?> EstimateArrivalAsync(ServiceVisit visit, DateTime now, CancellationToken cancellationToken)
    {
        var lead = await _resources.FindWorkerAsync(visit.LeadWorkerId, cancellationToken);

        if (lead is null)
        {
            return null;
        }

        var here = await _resources.PlaceOfAsync(visit.WorkOrderId, cancellationToken);
        var earlier = (await _resources.ListWorkerVisitsAsync(lead.ItemId, now.AddHours(-12), now, visit.ItemId, cancellationToken))
            .Where(other => other.EndUtc <= now || other.Status is VisitStatus.Completed or VisitStatus.Incomplete)
            .OrderBy(other => other.EndUtc)
            .LastOrDefault();
        var from = earlier is null ? (await _resources.GetAvailabilityAsync(lead, now, now.AddMinutes(1), visit.ItemId, cancellationToken)).Home : await _resources.PlaceOfAsync(earlier.WorkOrderId, cancellationToken);
        var trip = await _resources.TravelAsync(from, here, now, cancellationToken);

        return trip is null ? null : now + trip.Value.Duration;
    }

    private string Describe(string name, SlotProblem problem, string timeZoneId) => problem.Kind switch
    {
        SlotProblemKind.Overlaps => S["{0} has {1} from {2} to {3}.", name, problem.Visit.Number, Format(problem.Visit.StartUtc.Value, timeZoneId), Format(problem.Visit.EndUtc.Value, timeZoneId)],
        SlotProblemKind.TimeOff => S["{0} is on time off then.", name],
        SlotProblemKind.OffShift => S["{0} isn't on shift then, or is on a break.", name],
        SlotProblemKind.TooLateFromBefore => S["{0} can't get there in time after {1}, which ends at {2}.", name, problem.Visit.Number, Format(problem.Visit.EndUtc.Value, timeZoneId)],
        _ => S["{0} can't get to {1} at {2} in time after this.", name, problem.Visit.Number, Format(problem.Visit.StartUtc.Value, timeZoneId)],
    };

    private static string Format(DateTime utc, string timeZoneId)
        => CalendarTime.ToLocal(utc, timeZoneId).ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture);

    private static string Title(ServiceVisit visit)
        => string.IsNullOrWhiteSpace(visit.Summary) ? visit.WorkOrderNumber ?? visit.Number : $"{visit.WorkOrderNumber}: {visit.Summary}";

    private static DateTime RoundDown(DateTime value, int minutes)
    {
        var step = TimeSpan.FromMinutes(minutes).Ticks;

        return new DateTime(value.Ticks / step * step, value.Kind);
    }

    private async Task MoveWorkOrderAsync(string workOrderId, WorkOrderStatus status, string actor, CancellationToken cancellationToken)
    {
        var workOrder = await _workOrderStore.FindByIdAsync(workOrderId, cancellationToken);

        if (workOrder is null || workOrder.Status == status || !WorkOrderStatusRules.CanMove(workOrder.Status, status))
        {
            return;
        }

        // Scheduling never takes a started job back; on site moves it on from scheduled.
        if (status == WorkOrderStatus.Scheduled && workOrder.Status == WorkOrderStatus.InProgress)
        {
            return;
        }

        var result = await _workOrders.Value.ChangeStatusAsync(workOrderId, status, null, null, actor, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Work order {WorkOrderNumber} stayed {Status}: {Errors}", workOrder.Number, workOrder.Status, string.Join(" ", result.Errors));
        }
    }

    private async Task ReleaseWorkOrderAsync(ServiceVisit visit, string actor, CancellationToken cancellationToken)
    {
        var others = await ListForWorkOrderAsync(visit.WorkOrderId, cancellationToken);

        if (others.Any(other => other.ItemId != visit.ItemId && other.StartUtc is not null && VisitStatusRules.IsOpen(other.Status)))
        {
            return;
        }

        var workOrder = await _workOrderStore.FindByIdAsync(visit.WorkOrderId, cancellationToken);

        if (workOrder?.Status == WorkOrderStatus.Scheduled)
        {
            await MoveWorkOrderAsync(visit.WorkOrderId, WorkOrderStatus.Ready, actor, cancellationToken);
        }
    }

    private async Task<string> NextNumberAsync(CancellationToken cancellationToken)
    {
        var numbers = await _session.QueryIndex<ServiceVisitIndex>(index => index.Number.StartsWith(DispatchConstants.VisitNumberPrefix), collection: DispatchConstants.CollectionName).ListAsync(cancellationToken);
        var last = numbers
            .Select(index => int.TryParse(index.Number.AsSpan(DispatchConstants.VisitNumberPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return DispatchConstants.VisitNumberPrefix + (last + 1).ToString("D6", CultureInfo.InvariantCulture);
    }

    private async Task NotifyAsync(ServiceVisit visit, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnEventAsync(visit, eventName, detail ?? new Dictionary<string, object>(), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A visit observer failed on {EventName} for visit {VisitNumber}.", eventName, visit.Number);
            }
        }
    }

    private async Task<List<ILocker>> LockAsync(IEnumerable<string> keys)
    {
        var lockers = new List<ILocker>();

        foreach (var key in keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key, _lockTimeout, _lockExpiration);

            if (!locked)
            {
                await ReleaseAsync(lockers);

                return null;
            }

            lockers.Add(locker);
        }

        return lockers;
    }

    private static async Task ReleaseAsync(IEnumerable<ILocker> lockers)
    {
        foreach (var locker in lockers ?? [])
        {
            await locker.DisposeAsync();
        }
    }
}
