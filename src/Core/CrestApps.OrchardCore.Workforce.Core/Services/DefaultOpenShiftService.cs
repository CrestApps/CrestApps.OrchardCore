using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IOpenShiftService"/>.
/// </summary>
internal sealed class DefaultOpenShiftService : IOpenShiftService
{
    private const int MaxShiftHours = 24;

    private readonly OpenShiftStore _openShiftStore;
    private readonly TeamStore _teamStore;
    private readonly WorkerStore _workerStore;
    private readonly SchedulePeriodStore _periodStore;
    private readonly DraftShiftStore _draftStore;
    private readonly ScheduleChangeStore _changeStore;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly WorkforceCalendarService _calendarService;
    private readonly ICalendarService _calendars;
    private readonly ICalendarEventManager _eventManager;
    private readonly IShiftService _shiftService;
    private readonly WorkerLocks _locks;
    private readonly ShiftAssignmentContexts _contexts;
    private readonly OpenShiftPosting _posting;
    private readonly TeamPlanningPolicy _policy;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultOpenShiftService(
        OpenShiftStore openShiftStore,
        TeamStore teamStore,
        WorkerStore workerStore,
        SchedulePeriodStore periodStore,
        DraftShiftStore draftStore,
        ScheduleChangeStore changeStore,
        ActivityCodeStore activityCodeStore,
        WorkforceCalendarService calendarService,
        ICalendarService calendars,
        ICalendarEventManager eventManager,
        IShiftService shiftService,
        WorkerLocks locks,
        ShiftAssignmentContexts contexts,
        OpenShiftPosting posting,
        TeamPlanningPolicy policy,
        TeamTimeZoneResolver timeZones,
        IClock clock,
        IStringLocalizer<DefaultOpenShiftService> stringLocalizer)
    {
        _openShiftStore = openShiftStore;
        _teamStore = teamStore;
        _workerStore = workerStore;
        _periodStore = periodStore;
        _draftStore = draftStore;
        _changeStore = changeStore;
        _activityCodeStore = activityCodeStore;
        _calendarService = calendarService;
        _calendars = calendars;
        _eventManager = eventManager;
        _shiftService = shiftService;
        _locks = locks;
        _contexts = contexts;
        _posting = posting;
        _policy = policy;
        _timeZones = timeZones;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<PlanningResult<OpenShift>> PostAsync(OpenShift shift, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shift);

        var result = new PlanningResult<OpenShift>();
        var team = string.IsNullOrEmpty(shift.TeamId) ? null : await _teamStore.FindByIdAsync(shift.TeamId, cancellationToken);

        if (team is null)
        {
            result.Fail(S["Choose a team."]);
        }

        if (shift.End <= shift.Start || (shift.End - shift.Start).TotalHours > MaxShiftHours)
        {
            result.Fail(S["A shift must end after it starts, and last at most {0} hours.", MaxShiftHours]);
        }

        if (shift.Date == default || DateOnly.FromDateTime(shift.Start) != shift.Date)
        {
            result.Fail(S["The shift must start on its day."]);
        }

        if (shift.Segments.Any(segment => segment.Start < shift.Start || segment.End > shift.End || segment.End <= segment.Start))
        {
            result.Fail(S["Every break or activity must fit inside the shift."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var now = _clock.UtcNow;

        shift.ItemId = UniqueId.GenerateId();
        shift.PeriodId = null;
        shift.ShiftKey = UniqueId.GenerateId();
        shift.TimeZoneId = await _timeZones.GetAsync(team.ItemId, cancellationToken);
        shift.ClaimPolicy = await _policy.ClaimPolicyAsync(team.ItemId, cancellationToken);
        shift.Status = OpenShiftStatus.Open;
        shift.Claims = [];
        shift.FilledWorkerId = null;
        shift.CalendarEventId = null;
        shift.CreatedBy = actor;
        shift.CreatedUtc = now;
        shift.ClaimDeadlineUtc = _posting.Deadline(shift, now);

        await _openShiftStore.CreateAsync(shift, cancellationToken);
        result.Value = shift;

        return result;
    }

    public async Task<OpenShift> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _openShiftStore.FindByIdAsync(id, cancellationToken);
    }

    public Task<(int Count, IReadOnlyList<OpenShift> Items)> PageAsync(OpenShiftQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _openShiftStore.PageAsync(query ?? new OpenShiftQuery(), skip, take, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> CheckAsync(string id, string workerId, CancellationToken cancellationToken = default)
    {
        var shift = await FindAsync(id, cancellationToken);

        if (shift is null)
        {
            return [S["The open shift doesn't exist."]];
        }

        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerStore.FindByIdAsync(workerId, cancellationToken);

        return await CheckAsync(shift, worker, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkerProfile>> ListEligibleWorkersAsync(string id, CancellationToken cancellationToken = default)
    {
        var shift = await FindAsync(id, cancellationToken);

        if (shift is null)
        {
            return [];
        }

        var eligible = new List<WorkerProfile>();

        foreach (var worker in await _workerStore.ListByTeamAsync(shift.TeamId, WorkforceDates.ToKey(shift.Date), cancellationToken))
        {
            if (worker.Status == WorkerStatus.Active && (await CheckAsync(shift, worker, cancellationToken)).Count == 0)
            {
                eligible.Add(worker);
            }
        }

        return eligible;
    }

    public async Task<PlanningResult<OpenShift>> ClaimAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default)
    {
        var (result, shift, worker) = await LoadAsync(id, workerId, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        if (shift.Claims.Any(claim => claim.WorkerId == worker.ItemId && claim.Status == OpenShiftClaimStatus.Pending))
        {
            result.Fail(S["{0} has already claimed this shift.", worker.DisplayName]);

            return result;
        }

        foreach (var problem in await CheckAsync(shift, worker, cancellationToken))
        {
            result.Fail(problem);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var now = _clock.UtcNow;
        var claim = new OpenShiftClaim { WorkerId = worker.ItemId, Status = OpenShiftClaimStatus.Pending, ClaimedUtc = now };

        shift.Claims.Add(claim);

        if (shift.ClaimPolicy == OpenShiftClaimPolicy.FirstCome)
        {
            await FillAsync(shift, worker, ScheduleChangeInitiator.Worker, actor, result, cancellationToken);

            return result;
        }

        shift.Status = OpenShiftStatus.Claimed;
        await _openShiftStore.UpdateAsync(shift, cancellationToken);
        result.Value = shift;

        return result;
    }

    public async Task<PlanningResult<OpenShift>> WithdrawClaimAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<OpenShift>();
        var shift = await FindAsync(id, cancellationToken);
        var claim = shift?.Claims.FirstOrDefault(claim => claim.WorkerId == workerId && claim.Status == OpenShiftClaimStatus.Pending);

        if (claim is null)
        {
            result.Fail(S["There is no claim to withdraw."]);

            return result;
        }

        claim.Status = OpenShiftClaimStatus.Withdrawn;
        claim.DecidedBy = actor;
        claim.DecidedUtc = _clock.UtcNow;
        Settle(shift);
        await _openShiftStore.UpdateAsync(shift, cancellationToken);
        result.Value = shift;

        return result;
    }

    public async Task<PlanningResult<OpenShift>> AssignAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default)
    {
        var (result, shift, worker) = await LoadAsync(id, workerId, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        foreach (var problem in await CheckAsync(shift, worker, cancellationToken))
        {
            result.Fail(problem);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var claimed = shift.Claims.Any(claim => claim.WorkerId == worker.ItemId && claim.Status == OpenShiftClaimStatus.Pending);

        await FillAsync(shift, worker, claimed ? ScheduleChangeInitiator.Worker : ScheduleChangeInitiator.Employer, actor, result, cancellationToken);

        return result;
    }

    public async Task<PlanningResult<OpenShift>> DeclineClaimAsync(string id, string workerId, string note, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<OpenShift>();
        var shift = await FindAsync(id, cancellationToken);
        var claim = shift?.Claims.FirstOrDefault(claim => claim.WorkerId == workerId && claim.Status == OpenShiftClaimStatus.Pending);

        if (claim is null)
        {
            result.Fail(S["There is no claim to decline."]);

            return result;
        }

        claim.Status = OpenShiftClaimStatus.Declined;
        claim.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        claim.DecidedBy = actor;
        claim.DecidedUtc = _clock.UtcNow;
        Settle(shift);
        await _openShiftStore.UpdateAsync(shift, cancellationToken);
        result.Value = shift;

        return result;
    }

    public async Task<bool> CancelAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        var shift = await FindAsync(id, cancellationToken);

        if (shift is null || shift.Status is OpenShiftStatus.Filled or OpenShiftStatus.Canceled)
        {
            return false;
        }

        OpenShiftPosting.Cancel(shift, actor, _clock.UtcNow);
        await _openShiftStore.UpdateAsync(shift, cancellationToken);

        return true;
    }

    public async Task<int> AwardDueAsync(CancellationToken cancellationToken = default)
    {
        var awarded = 0;

        foreach (var shift in await _openShiftStore.ListDueAsync(_clock.UtcNow, cancellationToken))
        {
            var claimants = shift.Claims.Where(claim => claim.Status == OpenShiftClaimStatus.Pending).ToArray();
            var workers = (await _workerStore.GetAsync(claimants.Select(claim => claim.WorkerId), cancellationToken)).ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
            var filled = false;

            // The most senior first: seniority date, then hire date, then whoever claimed first.
            foreach (var claim in claimants
                .Where(claim => workers.ContainsKey(claim.WorkerId))
                .OrderBy(claim => workers[claim.WorkerId].SeniorityDate ?? workers[claim.WorkerId].HireDate ?? DateOnly.MaxValue)
                .ThenBy(claim => claim.ClaimedUtc))
            {
                var worker = workers[claim.WorkerId];

                if ((await CheckAsync(shift, worker, cancellationToken)).Count > 0)
                {
                    continue;
                }

                var result = new PlanningResult<OpenShift>();

                await FillAsync(shift, worker, ScheduleChangeInitiator.Worker, WorkforceConstants.Source, result, cancellationToken);

                if (result.Succeeded)
                {
                    awarded++;
                    filled = true;

                    break;
                }
            }

            if (!filled)
            {
                // Nobody eligible claimed it in time: leave it to a manager.
                shift.ClaimDeadlineUtc = null;
                await _openShiftStore.UpdateAsync(shift, cancellationToken);
            }
        }

        return awarded;
    }

    private async Task<(PlanningResult<OpenShift> Result, OpenShift Shift, WorkerProfile Worker)> LoadAsync(string id, string workerId, CancellationToken cancellationToken)
    {
        var result = new PlanningResult<OpenShift>();
        var shift = await FindAsync(id, cancellationToken);
        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerStore.FindByIdAsync(workerId, cancellationToken);

        if (shift is null)
        {
            result.Fail(S["The open shift doesn't exist."]);
        }
        else if (shift.Status is OpenShiftStatus.Filled or OpenShiftStatus.Canceled)
        {
            result.Fail(S["The shift is no longer offered."]);
        }

        if (worker is null)
        {
            result.Fail(S["The worker doesn't exist."]);
        }

        return (result, shift, worker);
    }

    private async Task<IReadOnlyList<string>> CheckAsync(OpenShift shift, WorkerProfile worker, CancellationToken cancellationToken)
    {
        if (worker is null)
        {
            return [S["The worker doesn't exist."]];
        }

        return await _shiftService.CheckAsync(await _contexts.ForOpenShiftAsync(shift, worker, cancellationToken), cancellationToken);
    }

    private async Task FillAsync(OpenShift shift, WorkerProfile worker, ScheduleChangeInitiator initiatedBy, string actor, PlanningResult<OpenShift> result, CancellationToken cancellationToken)
    {
        var lockers = await _locks.LockAsync([worker.ItemId]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing {0}'s shifts. Try again.", worker.DisplayName]);

            return;
        }

        try
        {
            var team = await _teamStore.FindByIdAsync(shift.TeamId, cancellationToken);

            if (team is null)
            {
                result.Fail(S["The team doesn't exist."]);

                return;
            }

            await _calendarService.EnsureCalendarAsync(team, cancellationToken);

            var calendar = await _calendars.FindByIdAsync(team.CalendarId, cancellationToken);
            var period = string.IsNullOrEmpty(shift.PeriodId) ? null : await _periodStore.FindByIdAsync(shift.PeriodId, cancellationToken);
            var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
            var data = ShiftEvents.ToShiftData(worker.ItemId, shift.TeamId, shift.PeriodId, shift.TemplateId, shift.ShiftKey, period?.PublishedVersion ?? 0, shift.Segments, codes, shift.TimeZoneId);
            var calendarEvent = await _eventManager.NewAsync(calendar, WorkforceConstants.EventTypes.Shift, cancellationToken: cancellationToken);

            ShiftEvents.Apply(calendarEvent, worker, shift.Start, shift.End, shift.Notes, data, shift.TimeZoneId);
            calendarEvent.Source = WorkforceConstants.Source;
            calendarEvent.SourceId = shift.ShiftKey;
            calendarEvent.Editable = false;

            var created = await _eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true }, cancellationToken);

            if (!created.Succeeded)
            {
                result.Fail(S["The shift could not be put on the calendar: {0}", string.Join(" ", created.Errors.Select(error => error.Message))]);

                return;
            }

            if (period is not null)
            {
                // The schedule's draft gets the worker, so the next publish finds the shift already right.
                var draft = await _draftStore.FindByShiftKeyAsync(period.ItemId, shift.Date, shift.ShiftKey, cancellationToken);

                if (draft is not null)
                {
                    draft.WorkerId = worker.ItemId;
                    await _draftStore.UpdateAsync(draft, cancellationToken);
                }
            }

            var now = _clock.UtcNow;

            foreach (var claim in shift.Claims.Where(claim => claim.Status == OpenShiftClaimStatus.Pending))
            {
                claim.Status = claim.WorkerId == worker.ItemId ? OpenShiftClaimStatus.Approved : OpenShiftClaimStatus.Declined;
                claim.DecidedBy = actor;
                claim.DecidedUtc = now;
            }

            shift.Status = OpenShiftStatus.Filled;
            shift.FilledWorkerId = worker.ItemId;
            shift.FilledUtc = now;
            shift.CalendarEventId = (created.Value ?? calendarEvent).ItemId;
            await _openShiftStore.UpdateAsync(shift, cancellationToken);

            await _changeStore.CreateAsync(new ScheduleChange
            {
                ItemId = UniqueId.GenerateId(),
                PeriodId = shift.PeriodId,
                TeamId = shift.TeamId,
                WorkerId = worker.ItemId,
                ShiftKey = shift.ShiftKey,
                Kind = ScheduleChangeKind.OpenShiftFilled,
                AfterStartUtc = CalendarTime.ToUtc(shift.Start, shift.TimeZoneId),
                AfterEndUtc = CalendarTime.ToUtc(shift.End, shift.TimeZoneId),
                InitiatedBy = initiatedBy,
                PublishedVersion = period?.PublishedVersion ?? 0,
                ChangedBy = actor,
                ChangedUtc = now,
            }, cancellationToken);

            result.Value = shift;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    private static void Settle(OpenShift shift)
    {
        if (shift.Status == OpenShiftStatus.Claimed && !shift.Claims.Any(claim => claim.Status == OpenShiftClaimStatus.Pending))
        {
            shift.Status = OpenShiftStatus.Open;
        }
    }
}
