using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeOffRequestService"/>. A submission runs under the worker's lock and the locks of the
/// allotments it falls under, and is saved before they are let go, so two requests for the last place can't both get
/// it.
/// </summary>
internal sealed class DefaultTimeOffRequestService : ITimeOffRequestService
{
    private const int MaxDays = 366;

    private readonly WorkerStore _workerStore;
    private readonly TeamStore _teamStore;
    private readonly TimeOffTypeStore _typeStore;
    private readonly TimeOffRequestStore _requestStore;
    private readonly BlackoutPeriodStore _blackoutStore;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly TimeOffPolicyResolvers _resolvers;
    private readonly ITimeOffBalanceService _balanceService;
    private readonly TimeOffAllotments _allotments;
    private readonly TimeOffApprovals _approvals;
    private readonly TimeOffLocks _locks;
    private readonly WorkerShiftReader _shiftReader;
    private readonly WorkerTimeZones _timeZones;
    private readonly IWorkforceEvents _events;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultTimeOffRequestService(
        WorkerStore workerStore,
        TeamStore teamStore,
        TimeOffTypeStore typeStore,
        TimeOffRequestStore requestStore,
        BlackoutPeriodStore blackoutStore,
        ActivityCodeStore activityCodeStore,
        TimeOffPolicyResolvers resolvers,
        ITimeOffBalanceService balanceService,
        TimeOffAllotments allotments,
        TimeOffApprovals approvals,
        TimeOffLocks locks,
        WorkerShiftReader shiftReader,
        WorkerTimeZones timeZones,
        IWorkforceEvents events,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultTimeOffRequestService> stringLocalizer)
    {
        _workerStore = workerStore;
        _teamStore = teamStore;
        _typeStore = typeStore;
        _requestStore = requestStore;
        _blackoutStore = blackoutStore;
        _activityCodeStore = activityCodeStore;
        _resolvers = resolvers;
        _balanceService = balanceService;
        _allotments = allotments;
        _approvals = approvals;
        _locks = locks;
        _shiftReader = shiftReader;
        _timeZones = timeZones;
        _events = events;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<TimeOffPreview> PreviewAsync(TimeOffRequestInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return (await BuildAsync(input, cancellationToken)).Preview;
    }

    public async Task<PlanningResult<TimeOffRequest>> SubmitAsync(TimeOffRequestInput input, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new PlanningResult<TimeOffRequest>();
        var worker = await _workerStore.FindByIdAsync(input.WorkerId, cancellationToken);
        var type = string.IsNullOrEmpty(input.TypeId) ? null : await _typeStore.FindByIdAsync(input.TypeId, cancellationToken);
        var allotments = worker is null || type is null
            ? []
            : await _allotments.ListForAsync(WorkerTimeZones.TeamOn(worker, input.StartDate), type, cancellationToken);
        var lockers = await _locks.LockAsync([TimeOffLocks.Worker(input.WorkerId), .. allotments.Select(allotment => TimeOffLocks.Allotment(allotment.ItemId))]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is asking for time off on these days. Try again."]);

            return result;
        }

        try
        {
            var built = await BuildAsync(input, cancellationToken);
            var preview = built.Preview;

            foreach (var problem in preview.Problems)
            {
                result.Fail(problem);
            }

            if (!result.Succeeded)
            {
                return result;
            }

            var now = _clock.UtcNow;
            var request = new TimeOffRequest
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = built.Worker.ItemId,
                TeamId = preview.TeamId,
                TypeId = preview.Type.ItemId,
                PolicyId = preview.Policy?.ItemId,
                StartDate = input.StartDate,
                EndDate = input.EndDate,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                TimeZoneId = preview.TimeZoneId,
                Days = preview.Days.ToList(),
                Amount = preview.Amount,
                Reason = input.Reason?.Trim(),
                SubmittedBy = actor,
                SubmittedUtc = now,
                CreatedUtc = now,
            };

            // Read again under the locks: another request may have taken the last place since the preview.
            var full = await _allotments.FullAsync(built.Allotments, request.Days.ToArray(), null, cancellationToken);
            var (status, automatic) = TimeOffAllotments.Decide(built.Allotments, full);

            if (status == TimeOffRequestStatus.Approved)
            {
                foreach (var error in await _approvals.ApproveAsync(request, built.Worker, preview.Type, actor, automatic, null, cancellationToken))
                {
                    result.Fail(error);
                }

                if (!result.Succeeded)
                {
                    return result;
                }
            }
            else
            {
                request.Status = status;
            }

            await _requestStore.CreateAsync(request, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
            result.Value = request;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }

        if (result.Value is { } submitted)
        {
            await _events.RaiseAsync(WorkforceEvents.TimeOffRequested, WorkforceEventInputs.TimeOff(submitted), submitted.ItemId, cancellationToken);

            if (submitted.Status == TimeOffRequestStatus.Approved)
            {
                await _events.RaiseAsync(WorkforceEvents.TimeOffApproved, WorkforceEventInputs.TimeOff(submitted), submitted.ItemId, cancellationToken);
            }
        }

        return result;
    }

    public async Task<PlanningResult<TimeOffRequest>> ApproveAsync(string id, string actor, string note = null, CancellationToken cancellationToken = default)
    {
        var approved = await DecideAsync(id, actor, async (request, worker, type, result) =>
        {
            foreach (var error in await _approvals.ApproveAsync(request, worker, type, actor, false, note?.Trim(), cancellationToken))
            {
                result.Fail(error);
            }
        }, cancellationToken);

        await RaiseAsync(approved, WorkforceEvents.TimeOffApproved, cancellationToken);

        return approved;
    }

    public async Task<PlanningResult<TimeOffRequest>> DeclineAsync(string id, string actor, string note = null, CancellationToken cancellationToken = default)
    {
        var declined = await DecideAsync(id, actor, (request, worker, type, result) =>
        {
            var now = _clock.UtcNow;

            request.Status = TimeOffRequestStatus.Declined;
            request.DecidedBy = actor;
            request.DecidedUtc = now;
            request.DecisionNote = note?.Trim();
            request.ModifiedUtc = now;

            return _approvals.NotifyAsync(request, type, S["Your {0} request was declined", type.Name], note, cancellationToken);
        }, cancellationToken);

        await RaiseAsync(declined, WorkforceEvents.TimeOffDeclined, cancellationToken);

        return declined;
    }

    private Task RaiseAsync(PlanningResult<TimeOffRequest> result, string name, CancellationToken cancellationToken)
    {
        return result.Succeeded && result.Value is { } request
            ? _events.RaiseAsync(name, WorkforceEventInputs.TimeOff(request), request.ItemId, cancellationToken)
            : Task.CompletedTask;
    }

    public async Task<PlanningResult<TimeOffRequest>> CancelAsync(string id, string actor, string note = null, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<TimeOffRequest>();
        var request = string.IsNullOrEmpty(id) ? null : await _requestStore.FindByIdAsync(id, cancellationToken);

        if (request is null)
        {
            result.Fail(S["The request doesn't exist."]);

            return result;
        }

        if (!request.IsOpen && request.Status != TimeOffRequestStatus.Approved)
        {
            result.Fail(S["Only an open request or approved time off can be canceled."]);

            return result;
        }

        var lockers = await _locks.LockAsync([TimeOffLocks.Worker(request.WorkerId)]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing this time off. Try again."]);

            return result;
        }

        var heldPlace = request.Status is TimeOffRequestStatus.Approved or TimeOffRequestStatus.Pending;

        try
        {
            var now = _clock.UtcNow;

            if (request.Status == TimeOffRequestStatus.Approved)
            {
                await _approvals.UndoAsync(request, actor, cancellationToken);
                request.Status = TimeOffRequestStatus.Canceled;

                if (await _typeStore.FindByIdAsync(request.TypeId, cancellationToken) is { } type)
                {
                    await _approvals.NotifyAsync(request, type, S["Your {0} was canceled", type.Name], note, cancellationToken);
                }
            }
            else
            {
                request.Status = TimeOffRequestStatus.Withdrawn;
            }

            request.DecidedBy = actor;
            request.DecidedUtc = now;
            request.DecisionNote = note?.Trim() ?? request.DecisionNote;
            request.ModifiedUtc = now;
            await _requestStore.UpdateAsync(request, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
            result.Value = request;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }

        await RaiseAsync(result, WorkforceEvents.TimeOffCanceled, cancellationToken);

        if (heldPlace)
        {
            await PromoteAsync(request, actor, cancellationToken);
        }

        return result;
    }

    public async Task<TimeOffRequest> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _requestStore.FindByIdAsync(id, cancellationToken);
    }

    public Task<(int Count, IReadOnlyList<TimeOffRequest> Items)> PageAsync(TimeOffRequestQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _requestStore.PageAsync(query, skip, take, cancellationToken);
    }

    private async Task<PlanningResult<TimeOffRequest>> DecideAsync(string id, string actor, Func<TimeOffRequest, WorkerProfile, TimeOffType, PlanningResult<TimeOffRequest>, Task> decide, CancellationToken cancellationToken)
    {
        var result = new PlanningResult<TimeOffRequest>();
        var request = string.IsNullOrEmpty(id) ? null : await _requestStore.FindByIdAsync(id, cancellationToken);

        if (request is null)
        {
            result.Fail(S["The request doesn't exist."]);

            return result;
        }

        if (!request.IsOpen)
        {
            result.Fail(S["This request has already been decided."]);

            return result;
        }

        var worker = await _workerStore.FindByIdAsync(request.WorkerId, cancellationToken);
        var type = await _typeStore.FindByIdAsync(request.TypeId, cancellationToken);

        if (worker is null || type is null)
        {
            result.Fail(S["The worker or the time-off type no longer exists."]);

            return result;
        }

        var lockers = await _locks.LockAsync([TimeOffLocks.Worker(request.WorkerId)]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing {0}'s time off. Try again.", worker.DisplayName]);

            return result;
        }

        try
        {
            await decide(request, worker, type, result);

            if (!result.Succeeded)
            {
                return result;
            }

            await _requestStore.UpdateAsync(request, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
            result.Value = request;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }

        return result;
    }

    // Gives the places a canceled request freed to the requests waiting for them, the earliest first.
    private async Task PromoteAsync(TimeOffRequest freed, string actor, CancellationToken cancellationToken)
    {
        var type = await _typeStore.FindByIdAsync(freed.TypeId, cancellationToken);
        var allotments = await _allotments.ListForAsync(freed.TeamId, type, cancellationToken);

        if (allotments.Count == 0)
        {
            return;
        }

        var teams = await _teamStore.ListAsync(cancellationToken);
        var waiting = new List<TimeOffRequest>();

        foreach (var allotment in allotments)
        {
            var subtree = await TeamTree.GetSubtreeIdsAsync(teams, allotment.TeamId);

            waiting.AddRange(await _requestStore.ListForTeamsAsync(subtree.ToArray(), [TimeOffRequestStatus.Waitlisted], freed.StartDate, freed.EndDate, cancellationToken));
        }

        var candidates = waiting.DistinctBy(request => request.ItemId).OrderBy(request => request.SubmittedUtc).ToArray();

        if (candidates.Length == 0)
        {
            return;
        }

        var types = (await _typeStore.ListAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var plans = new List<(TimeOffRequest Request, TimeOffType Type, IReadOnlyList<TimeOffAllotment> Allotments)>();

        foreach (var candidate in candidates)
        {
            if (types.TryGetValue(candidate.TypeId ?? string.Empty, out var candidateType))
            {
                plans.Add((candidate, candidateType, await _allotments.ListForAsync(candidate.TeamId, candidateType, cancellationToken)));
            }
        }

        var keys = allotments.Concat(plans.SelectMany(plan => plan.Allotments)).Select(allotment => TimeOffLocks.Allotment(allotment.ItemId))
            .Concat(plans.Select(plan => TimeOffLocks.Worker(plan.Request.WorkerId)));
        var lockers = await _locks.LockAsync(keys);

        if (lockers is null)
        {
            return;
        }

        try
        {
            foreach (var plan in plans)
            {
                var request = await _requestStore.FindByIdAsync(plan.Request.ItemId, cancellationToken);
                var worker = request is null ? null : await _workerStore.FindByIdAsync(request.WorkerId, cancellationToken);

                if (request?.Status != TimeOffRequestStatus.Waitlisted || worker is null)
                {
                    continue;
                }

                var full = await _allotments.FullAsync(plan.Allotments, request.Days.ToArray(), request.ItemId, cancellationToken);

                if (full.Count > 0)
                {
                    continue;
                }

                var (status, automatic) = TimeOffAllotments.Decide(plan.Allotments, full);

                if (status == TimeOffRequestStatus.Approved)
                {
                    if ((await _approvals.ApproveAsync(request, worker, plan.Type, actor, automatic, null, cancellationToken)).Count > 0)
                    {
                        continue;
                    }
                }
                else
                {
                    request.Status = TimeOffRequestStatus.Pending;
                    request.ModifiedUtc = _clock.UtcNow;
                }

                await _requestStore.UpdateAsync(request, cancellationToken);

                // Saved one at a time, so the next request counts the place this one took.
                await _session.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }

    private async Task<Built> BuildAsync(TimeOffRequestInput input, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        var preview = new TimeOffPreview { Problems = problems };
        var built = new Built { Preview = preview };
        var worker = string.IsNullOrEmpty(input.WorkerId) ? null : await _workerStore.FindByIdAsync(input.WorkerId, cancellationToken);
        var type = string.IsNullOrEmpty(input.TypeId) ? null : await _typeStore.FindByIdAsync(input.TypeId, cancellationToken);

        built.Worker = worker;
        preview.Type = type;

        if (worker is null)
        {
            problems.Add(S["The worker doesn't exist."]);
        }
        else if (worker.Status == WorkerStatus.Terminated)
        {
            problems.Add(S["{0} no longer works here.", worker.DisplayName]);
        }

        if (type is null || !type.Enabled)
        {
            problems.Add(S["Choose a time-off type that is in use."]);
        }

        if (input.EndDate < input.StartDate)
        {
            problems.Add(S["The last day can't be before the first."]);
        }
        else if (input.EndDate.DayNumber - input.StartDate.DayNumber >= MaxDays)
        {
            problems.Add(S["A request can cover at most a year."]);
        }

        var partial = input.StartTime.HasValue || input.EndTime.HasValue;

        if (partial)
        {
            if (!input.StartTime.HasValue || !input.EndTime.HasValue || input.EndTime <= input.StartTime)
            {
                problems.Add(S["For part of a day, enter a start and an end time, the end after the start."]);
            }

            if (input.EndDate != input.StartDate)
            {
                problems.Add(S["Part of a day can be requested for one day only."]);
            }

            if (type is not null && !type.AllowPartialDay)
            {
                problems.Add(S["{0} is taken in whole days.", type.Name]);
            }
        }

        if (type?.RequiresReason == true && string.IsNullOrWhiteSpace(input.Reason))
        {
            problems.Add(S["{0} needs a reason.", type.Name]);
        }

        if (problems.Count > 0)
        {
            return built;
        }

        var teamId = WorkerTimeZones.TeamOn(worker, input.StartDate);

        if (string.IsNullOrEmpty(teamId))
        {
            problems.Add(S["{0} isn't on a team.", worker.DisplayName]);

            return built;
        }

        var zone = await _timeZones.GetAsync(worker, teamId, cancellationToken);
        var fromUtc = CalendarTime.ToUtc(input.StartDate.ToDateTime(input.StartTime ?? TimeOnly.MinValue), zone);
        var toUtc = input.EndTime.HasValue
            ? CalendarTime.ToUtc(input.EndDate.ToDateTime(input.EndTime.Value), zone)
            : CalendarTime.ToUtc(input.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var shifts = await _shiftReader.ListAsync(
            worker,
            CalendarTime.ToUtc(input.StartDate.ToDateTime(TimeOnly.MinValue), zone),
            CalendarTime.ToUtc(input.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone),
            cancellationToken);
        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);

        preview.TeamId = teamId;
        preview.TimeZoneId = zone;
        preview.Days = TimeOffDayCounter.Count(type, input.StartDate, input.EndDate, input.StartTime, input.EndTime, zone, shifts, codes);
        preview.OverlappingShifts = shifts.Count(shift => shift.StartUtc < toUtc && shift.EndUtc > fromUtc);

        if (preview.Amount <= 0)
        {
            problems.Add(S["These days count nothing: {0} has no shifts on them and they fall on a weekend.", worker.DisplayName]);
        }

        await CheckBlackoutsAsync(input, type, teamId, problems, cancellationToken);

        foreach (var other in await _requestStore.ListLiveForWorkerAsync(worker.ItemId, input.StartDate, input.EndDate, cancellationToken))
        {
            if (Overlaps(other, input))
            {
                problems.Add(S["{0} already has time off or a request from {1} to {2}.", worker.DisplayName, other.StartDate.ToString("d", null), other.EndDate.ToString("d", null)]);

                break;
            }
        }

        var resolver = await _resolvers.ForWorkerAsync(worker, cancellationToken);

        preview.Policy = resolver.Resolve(type.ItemId, input.StartDate);

        if (preview.Policy is not null)
        {
            preview.Balance = await _balanceService.GetBalanceAsync(worker.ItemId, preview.Policy.ItemId, input.StartDate, cancellationToken);
        }

        if (type.RequiresBalance)
        {
            if (preview.Policy is null)
            {
                problems.Add(S["{0} has no {1} policy, so there's no balance to take this from.", worker.DisplayName, type.Name]);
            }
            else if (preview.Amount > preview.Balance.Available + preview.Policy.NegativeBalanceLimit)
            {
                problems.Add(S["{0} doesn't have the balance for this: {1} available, {2} asked for.", worker.DisplayName, preview.Balance.Available.ToString("0.##", null), preview.Amount.ToString("0.##", null)]);
            }
        }

        built.Allotments = await _allotments.ListForAsync(teamId, type, cancellationToken);

        var full = await _allotments.FullAsync(built.Allotments, preview.Days, null, cancellationToken);

        preview.FullDates = full.Select(item => item.Date).Distinct().Order().ToArray();
        preview.Outcome = TimeOffAllotments.Decide(built.Allotments, full).Status;

        return built;
    }

    private async Task CheckBlackoutsAsync(TimeOffRequestInput input, TimeOffType type, string teamId, List<string> problems, CancellationToken cancellationToken)
    {
        var lineage = TimeOffAllotments.Lineage(await _teamStore.ListAsync(cancellationToken), teamId);

        foreach (var blackout in await _blackoutStore.ListAsync(cancellationToken))
        {
            if (blackout.Enabled
                && blackout.StartDate <= input.EndDate
                && blackout.EndDate >= input.StartDate
                && (string.IsNullOrEmpty(blackout.TeamId) || lineage.Contains(blackout.TeamId))
                && (blackout.TypeIds.Count == 0 || blackout.TypeIds.Contains(type.ItemId)))
            {
                problems.Add(string.IsNullOrWhiteSpace(blackout.Message)
                    ? S["Time off can't be requested from {0} to {1} ({2}).", blackout.StartDate.ToString("d", null), blackout.EndDate.ToString("d", null), blackout.Name]
                    : blackout.Message.Trim());
            }
        }
    }

    private static bool Overlaps(TimeOffRequest other, TimeOffRequestInput input)
    {
        if (other.StartDate > input.EndDate || other.EndDate < input.StartDate)
        {
            return false;
        }

        // Two parts of the same day overlap only when their times do.
        if (other.IsPartialDay && input.StartTime.HasValue && input.EndTime.HasValue && other.StartDate == input.StartDate)
        {
            return other.StartTime < input.EndTime && input.StartTime < other.EndTime;
        }

        return true;
    }

    private sealed class Built
    {
        public TimeOffPreview Preview { get; set; }

        public WorkerProfile Worker { get; set; }

        public IReadOnlyList<TimeOffAllotment> Allotments { get; set; } = [];
    }
}
