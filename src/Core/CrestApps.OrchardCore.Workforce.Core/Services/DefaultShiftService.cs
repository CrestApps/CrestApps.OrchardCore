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
/// The default <see cref="IShiftService"/>.
/// </summary>
internal sealed class DefaultShiftService : IShiftService
{
    private readonly ICalendarEventManager _eventManager;
    private readonly WorkerStore _workerStore;
    private readonly SchedulePeriodStore _periodStore;
    private readonly DraftShiftStore _draftStore;
    private readonly ScheduleChangeRecorder _changeRecorder;
    private readonly WorkerShiftReader _shiftReader;
    private readonly ShiftAssignmentContexts _contexts;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IEnumerable<IShiftAssignmentValidator> _validators;
    private readonly WorkerLocks _locks;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultShiftService(
        ICalendarEventManager eventManager,
        WorkerStore workerStore,
        SchedulePeriodStore periodStore,
        DraftShiftStore draftStore,
        ScheduleChangeRecorder changeRecorder,
        WorkerShiftReader shiftReader,
        ShiftAssignmentContexts contexts,
        TeamTimeZoneResolver timeZones,
        IEnumerable<IShiftAssignmentValidator> validators,
        WorkerLocks locks,
        IClock clock,
        IStringLocalizer<DefaultShiftService> stringLocalizer)
    {
        _eventManager = eventManager;
        _workerStore = workerStore;
        _periodStore = periodStore;
        _draftStore = draftStore;
        _changeRecorder = changeRecorder;
        _shiftReader = shiftReader;
        _contexts = contexts;
        _timeZones = timeZones;
        _validators = validators;
        _locks = locks;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<CalendarEvent>> ListWorkerShiftsAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerStore.FindByIdAsync(workerId, cancellationToken);

        return worker is null ? [] : await _shiftReader.ListAsync(worker, fromUtc, toUtc, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> CheckAsync(ShiftAssignmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var validator in _validators)
        {
            await validator.ValidateAsync(context, cancellationToken);
        }

        return context.Problems.Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<PlanningResult<CalendarEvent>> ReassignAsync(string eventId, string workerId, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<CalendarEvent>();
        var calendarEvent = string.IsNullOrEmpty(eventId) ? null : await _eventManager.FindByIdAsync(eventId, cancellationToken);
        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerStore.FindByIdAsync(workerId, cancellationToken);

        if (!ShiftEvents.TryRead(calendarEvent, out var data))
        {
            result.Fail(S["The shift doesn't exist."]);

            return result;
        }

        if (worker is null)
        {
            result.Fail(S["The worker doesn't exist."]);

            return result;
        }

        if (data.WorkerId == worker.ItemId)
        {
            result.Fail(S["{0} already works this shift.", worker.DisplayName]);

            return result;
        }

        var lockers = await _locks.LockAsync([data.WorkerId, worker.ItemId]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing these shifts. Try again."]);

            return result;
        }

        try
        {
            var context = await _contexts.ForEventAsync(calendarEvent, data, worker, [data.ShiftKey, calendarEvent.SourceId], cancellationToken);

            foreach (var problem in await CheckAsync(context, cancellationToken))
            {
                result.Fail(problem);
            }

            if (!result.Succeeded)
            {
                return result;
            }

            var errors = new List<string>();
            var changed = await MoveAsync(calendarEvent, data, worker, ScheduleChangeKind.Reassigned, initiatedBy, reason, actor, errors, cancellationToken);

            errors.ForEach(result.Fail);
            result.Value = changed;

            return result;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    public async Task<PlanningResult<IReadOnlyList<CalendarEvent>>> SwapAsync(string firstEventId, string secondEventId, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<IReadOnlyList<CalendarEvent>>();
        var first = string.IsNullOrEmpty(firstEventId) ? null : await _eventManager.FindByIdAsync(firstEventId, cancellationToken);
        var second = string.IsNullOrEmpty(secondEventId) ? null : await _eventManager.FindByIdAsync(secondEventId, cancellationToken);

        if (!ShiftEvents.TryRead(first, out var firstData) || !ShiftEvents.TryRead(second, out var secondData) || first.ItemId == second.ItemId)
        {
            result.Fail(S["The shifts don't exist."]);

            return result;
        }

        if (firstData.WorkerId == secondData.WorkerId)
        {
            result.Fail(S["Both shifts belong to the same worker."]);

            return result;
        }

        var firstWorker = await _workerStore.FindByIdAsync(firstData.WorkerId, cancellationToken);
        var secondWorker = await _workerStore.FindByIdAsync(secondData.WorkerId, cancellationToken);

        if (firstWorker is null || secondWorker is null)
        {
            result.Fail(S["The worker doesn't exist."]);

            return result;
        }

        var lockers = await _locks.LockAsync([firstWorker.ItemId, secondWorker.ItemId]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing these shifts. Try again."]);

            return result;
        }

        try
        {
            // Each worker gives up their own shift in the same change, so neither counts against the other's checks.
            string[] keys = [firstData.ShiftKey, first.SourceId, secondData.ShiftKey, second.SourceId];
            var problems = (await CheckAsync(await _contexts.ForEventAsync(first, firstData, secondWorker, keys, cancellationToken), cancellationToken))
                .Concat(await CheckAsync(await _contexts.ForEventAsync(second, secondData, firstWorker, keys, cancellationToken), cancellationToken));

            foreach (var problem in problems)
            {
                result.Fail(problem);
            }

            if (!result.Succeeded)
            {
                return result;
            }

            var errors = new List<string>();
            var movedFirst = await MoveAsync(first, firstData, secondWorker, ScheduleChangeKind.Swapped, initiatedBy, reason, actor, errors, cancellationToken);
            var movedSecond = errors.Count == 0 ? await MoveAsync(second, secondData, firstWorker, ScheduleChangeKind.Swapped, initiatedBy, reason, actor, errors, cancellationToken) : second;

            errors.ForEach(result.Fail);
            result.Value = [movedFirst, movedSecond];

            return result;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    private async Task<CalendarEvent> MoveAsync(CalendarEvent calendarEvent, ShiftData data, WorkerProfile worker, ScheduleChangeKind kind, ScheduleChangeInitiator initiatedBy, string reason, string actor, List<string> errors, CancellationToken cancellationToken)
    {
        var previousWorkerId = data.WorkerId;
        var options = new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
        var updated = await _eventManager.UpdateAsync(calendarEvent, target =>
        {
            ShiftEvents.Assign(target, worker);
            data.WorkerId = worker.ItemId;
            target.Put(data);
        }, options, cancellationToken);

        if (!updated.Succeeded)
        {
            errors.Add(S["The shift could not be changed: {0}", string.Join(" ", updated.Errors.Select(error => error.Message))]);

            return calendarEvent;
        }

        var period = string.IsNullOrEmpty(data.ScheduleId) ? null : await _periodStore.FindByIdAsync(data.ScheduleId, cancellationToken);

        if (period is not null)
        {
            // Keep the schedule's draft in step, so the next publish finds the shift already right.
            var timeZoneId = await _timeZones.GetAsync(data.TeamId, cancellationToken);
            var date = DateOnly.FromDateTime(CalendarTime.ToLocal(calendarEvent.StartUtc, timeZoneId));
            var draft = await _draftStore.FindByShiftKeyAsync(period.ItemId, date, data.ShiftKey ?? calendarEvent.SourceId, cancellationToken);

            if (draft is not null)
            {
                draft.WorkerId = worker.ItemId;
                await _draftStore.UpdateAsync(draft, cancellationToken);
            }
        }

        await _changeRecorder.RecordAsync(new ScheduleChange
        {
            ItemId = UniqueId.GenerateId(),
            PeriodId = data.ScheduleId,
            TeamId = data.TeamId,
            WorkerId = worker.ItemId,
            ShiftKey = data.ShiftKey ?? calendarEvent.SourceId,
            Kind = kind,
            BeforeStartUtc = calendarEvent.StartUtc,
            BeforeEndUtc = calendarEvent.EndUtc,
            AfterStartUtc = calendarEvent.StartUtc,
            AfterEndUtc = calendarEvent.EndUtc,
            PreviousWorkerId = previousWorkerId,
            InitiatedBy = initiatedBy,
            Reason = reason,
            PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
            ChangedBy = actor,
            ChangedUtc = _clock.UtcNow,
        }, cancellationToken);

        return updated.Value ?? calendarEvent;
    }
}
