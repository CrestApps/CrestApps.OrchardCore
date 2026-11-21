using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
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
    private readonly StaffMemberStore _workerStore;
    private readonly SchedulePeriodStore _periodStore;
    private readonly DraftShiftStore _draftStore;
    private readonly ScheduleChangeRecorder _changeRecorder;
    private readonly WorkerShiftReader _shiftReader;
    private readonly ShiftAssignmentContexts _contexts;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IEnumerable<IShiftAssignmentValidator> _validators;
    private readonly IActivityCodeManager _activityCodes;
    private readonly WorkerLocks _locks;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultShiftService(
        ICalendarEventManager eventManager,
        StaffMemberStore workerStore,
        SchedulePeriodStore periodStore,
        DraftShiftStore draftStore,
        ScheduleChangeRecorder changeRecorder,
        WorkerShiftReader shiftReader,
        ShiftAssignmentContexts contexts,
        TeamTimeZoneResolver timeZones,
        IEnumerable<IShiftAssignmentValidator> validators,
        IActivityCodeManager activityCodes,
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
        _activityCodes = activityCodes;
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

    public async Task<PlanningResult<CalendarEvent>> ChangeTimesAsync(string eventId, DateTime startUtc, DateTime endUtc, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<CalendarEvent>();
        var calendarEvent = string.IsNullOrEmpty(eventId) ? null : await _eventManager.FindByIdAsync(eventId, cancellationToken);

        if (!ShiftEvents.TryRead(calendarEvent, out var data))
        {
            result.Fail(S["The shift doesn't exist."]);

            return result;
        }

        if (endUtc <= startUtc)
        {
            result.Fail(S["The shift must end after it starts."]);

            return result;
        }

        var worker = await _workerStore.FindByIdAsync(data.WorkerId, cancellationToken);

        if (worker is null)
        {
            result.Fail(S["The worker doesn't exist."]);

            return result;
        }

        var lockers = await _locks.LockAsync([worker.ItemId]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing these shifts. Try again."]);

            return result;
        }

        try
        {
            var context = await _contexts.ForEventAsync(calendarEvent, data, worker, [data.ShiftKey, calendarEvent.SourceId], cancellationToken);
            var beforeStart = calendarEvent.StartUtc;
            var beforeEnd = calendarEvent.EndUtc;

            context.PaidMinutes += (int)Math.Round(((endUtc - startUtc) - (beforeEnd - beforeStart)).TotalMinutes);
            context.StartUtc = startUtc;
            context.EndUtc = endUtc;

            foreach (var problem in await CheckAsync(context, cancellationToken))
            {
                result.Fail(problem);
            }

            if (!result.Succeeded)
            {
                return result;
            }

            var timeZoneId = string.IsNullOrEmpty(calendarEvent.TimeZoneId) ? await _timeZones.GetAsync(data.TeamId, cancellationToken) : calendarEvent.TimeZoneId;
            var options = new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
            var updated = await _eventManager.UpdateAsync(calendarEvent, target =>
            {
                target.Start = CalendarTime.ToLocal(startUtc, timeZoneId);
                target.End = CalendarTime.ToLocal(endUtc, timeZoneId);
                target.StartUtc = startUtc;
                target.EndUtc = endUtc;
                data.Segments = [.. data.Segments.Where(segment => segment.StartUtc >= startUtc && segment.EndUtc <= endUtc)];
                target.Put(data);
            }, options, cancellationToken);

            if (!updated.Succeeded)
            {
                result.Fail(S["The shift could not be changed: {0}", string.Join(" ", updated.Errors.Select(error => error.Message))]);

                return result;
            }

            var period = string.IsNullOrEmpty(data.ScheduleId) ? null : await _periodStore.FindByIdAsync(data.ScheduleId, cancellationToken);

            if (period is not null)
            {
                // Keep the schedule's draft in step, so the next publish finds the shift already right.
                var date = DateOnly.FromDateTime(CalendarTime.ToLocal(beforeStart, timeZoneId));
                var draft = await _draftStore.FindByShiftKeyAsync(period.ItemId, date, data.ShiftKey ?? calendarEvent.SourceId, cancellationToken);

                if (draft is not null)
                {
                    draft.Start = CalendarTime.ToLocal(startUtc, timeZoneId);
                    draft.End = CalendarTime.ToLocal(endUtc, timeZoneId);
                    draft.Segments = [.. draft.Segments.Where(segment => segment.Start >= draft.Start && segment.End <= draft.End)];
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
                Kind = ScheduleChangeKind.Moved,
                BeforeStartUtc = beforeStart,
                BeforeEndUtc = beforeEnd,
                AfterStartUtc = startUtc,
                AfterEndUtc = endUtc,
                InitiatedBy = initiatedBy,
                Reason = reason,
                PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
                ChangedBy = actor,
                ChangedUtc = _clock.UtcNow,
            }, cancellationToken);

            result.Value = updated.Value ?? calendarEvent;

            return result;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    /// <inheritdoc/>
    public async Task<PlanningResult<CalendarEvent>> RemoveAsync(string eventId, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<CalendarEvent>();
        var calendarEvent = string.IsNullOrEmpty(eventId) ? null : await _eventManager.FindByIdAsync(eventId, cancellationToken);

        if (!ShiftEvents.TryRead(calendarEvent, out var data) || calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            result.Fail(S["The shift doesn't exist."]);

            return result;
        }

        var lockers = await _locks.LockAsync(string.IsNullOrEmpty(data.WorkerId) ? [] : [data.WorkerId]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing these shifts. Try again."]);

            return result;
        }

        try
        {
            var options = new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
            var cancelled = await _eventManager.CancelAsync(calendarEvent, options, cancellationToken);

            if (!cancelled.Succeeded)
            {
                result.Fail(S["The shift could not be removed: {0}", string.Join(" ", cancelled.Errors.Select(error => error.Message))]);

                return result;
            }

            var period = string.IsNullOrEmpty(data.ScheduleId) ? null : await _periodStore.FindByIdAsync(data.ScheduleId, cancellationToken);

            if (period is not null)
            {
                // The draft goes too, or the next publish would put the shift back.
                var timeZoneId = string.IsNullOrEmpty(calendarEvent.TimeZoneId) ? await _timeZones.GetAsync(data.TeamId, cancellationToken) : calendarEvent.TimeZoneId;
                var date = DateOnly.FromDateTime(CalendarTime.ToLocal(calendarEvent.StartUtc, timeZoneId));
                var draft = await _draftStore.FindByShiftKeyAsync(period.ItemId, date, data.ShiftKey ?? calendarEvent.SourceId, cancellationToken);

                if (draft is not null)
                {
                    await _draftStore.DeleteAsync(draft, cancellationToken);
                }
            }

            await _changeRecorder.RecordAsync(new ScheduleChange
            {
                ItemId = UniqueId.GenerateId(),
                PeriodId = data.ScheduleId,
                TeamId = data.TeamId,
                WorkerId = data.WorkerId,
                ShiftKey = data.ShiftKey ?? calendarEvent.SourceId,
                Kind = ScheduleChangeKind.Removed,
                BeforeStartUtc = calendarEvent.StartUtc,
                BeforeEndUtc = calendarEvent.EndUtc,
                InitiatedBy = initiatedBy,
                Reason = reason,
                PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
                ChangedBy = actor,
                ChangedUtc = _clock.UtcNow,
            }, cancellationToken);

            result.Value = cancelled.Value ?? calendarEvent;

            return result;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    /// <inheritdoc/>
    public async Task<PlanningResult<IReadOnlyList<CalendarEvent>>> PlaceSegmentAsync(ShiftSegmentPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);

        var result = new PlanningResult<IReadOnlyList<CalendarEvent>>();

        if (string.IsNullOrEmpty(placement.Source) || string.IsNullOrEmpty(placement.SourceId))
        {
            result.Fail(S["Say what places the segment, so it can be taken back."]);

            return result;
        }

        if (placement.EndUtc <= placement.StartUtc)
        {
            result.Fail(S["The segment must end after it starts."]);

            return result;
        }

        var worker = string.IsNullOrEmpty(placement.WorkerId) ? null : await _workerStore.FindByIdAsync(placement.WorkerId, cancellationToken);

        if (worker is null)
        {
            result.Fail(S["The worker doesn't exist."]);

            return result;
        }

        var codes = await _activityCodes.GetEnabledAsync(cancellationToken);
        var code = string.IsNullOrEmpty(placement.ActivityCodeId)
            ? codes.FirstOrDefault(item => item.Kind == placement.Kind)
            : codes.FirstOrDefault(item => item.ItemId == placement.ActivityCodeId);

        if (code is null)
        {
            result.Fail(string.IsNullOrEmpty(placement.ActivityCodeId)
                ? S["No enabled activity code is of the kind {0}. Add one under Workforce, Activity codes.", placement.Kind]
                : S["The activity code doesn't exist or is disabled."]);

            return result;
        }

        var lockers = await _locks.LockAsync([worker.ItemId]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing these shifts. Try again."]);

            return result;
        }

        try
        {
            var shifts = await ListCoveringShiftsAsync(worker, placement.StartUtc, placement.EndUtc, cancellationToken);

            if (shifts.Count == 0)
            {
                result.Fail(S["No published shift covers that time."]);

                return result;
            }

            var errors = new List<string>();
            var changed = new List<CalendarEvent>();

            foreach (var (calendarEvent, data) in shifts)
            {
                var segment = new ShiftSegment
                {
                    ActivityCodeId = code.ItemId,
                    Kind = code.Kind,
                    StartUtc = placement.StartUtc > calendarEvent.StartUtc ? placement.StartUtc : calendarEvent.StartUtc,
                    EndUtc = placement.EndUtc < calendarEvent.EndUtc ? placement.EndUtc : calendarEvent.EndUtc,
                    Source = placement.Source,
                    SourceId = placement.SourceId,
                };

                var updated = await ReplaceSourceSegmentAsync(calendarEvent, data, worker, placement.Source, placement.SourceId, segment, placement.Reason, placement.Actor, errors, cancellationToken);

                if (updated is not null)
                {
                    changed.Add(updated);
                }
            }

            errors.ForEach(result.Fail);
            result.Value = changed;

            return result;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    /// <inheritdoc/>
    public async Task<int> RemoveSegmentsAsync(string workerId, string source, string sourceId, DateTime fromUtc, DateTime toUtc, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceId) || toUtc <= fromUtc)
        {
            return 0;
        }

        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerStore.FindByIdAsync(workerId, cancellationToken);

        if (worker is null)
        {
            return 0;
        }

        var lockers = await _locks.LockAsync([worker.ItemId]);

        if (lockers is null)
        {
            throw new InvalidOperationException($"The shifts of worker '{worker.ItemId}' are being changed by someone else.");
        }

        try
        {
            var removed = 0;
            var errors = new List<string>();

            foreach (var (calendarEvent, data) in await ListCoveringShiftsAsync(worker, fromUtc, toUtc, cancellationToken))
            {
                var count = data.Segments.Count(segment => IsFrom(segment.Source, segment.SourceId, source, sourceId));

                if (count == 0)
                {
                    continue;
                }

                if (await ReplaceSourceSegmentAsync(calendarEvent, data, worker, source, sourceId, null, null, actor, errors, cancellationToken) is not null)
                {
                    removed += count;
                }
            }

            return removed;
        }
        finally
        {
            await WorkerLocks.ReleaseAsync(lockers);
        }
    }

    private async Task<IReadOnlyList<(CalendarEvent Event, ShiftData Data)>> ListCoveringShiftsAsync(StaffMember worker, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var shifts = new List<(CalendarEvent, ShiftData)>();

        foreach (var calendarEvent in await _shiftReader.ListAsync(worker, fromUtc, toUtc, cancellationToken))
        {
            if (calendarEvent.StartUtc < toUtc && calendarEvent.EndUtc > fromUtc && ShiftEvents.TryRead(calendarEvent, out var data))
            {
                shifts.Add((calendarEvent, data));
            }
        }

        return shifts;
    }

    /// <summary>
    /// Takes a source's segments off a shift and its draft and puts <paramref name="added"/> in their place, when given.
    /// Returns the changed event, or <see langword="null"/> when the change failed.
    /// </summary>
    private async Task<CalendarEvent> ReplaceSourceSegmentAsync(CalendarEvent calendarEvent, ShiftData data, StaffMember worker, string source, string sourceId, ShiftSegment added, string reason, string actor, List<string> errors, CancellationToken cancellationToken)
    {
        var timeZoneId = string.IsNullOrEmpty(calendarEvent.TimeZoneId) ? await _timeZones.GetAsync(data.TeamId, cancellationToken) : calendarEvent.TimeZoneId;
        var segments = data.Segments.Where(segment => !IsFrom(segment.Source, segment.SourceId, source, sourceId)).ToList();

        if (added is not null)
        {
            segments.Add(added);
        }

        var options = new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
        var updated = await _eventManager.UpdateAsync(calendarEvent, target =>
        {
            data.Segments = [.. segments.OrderBy(segment => segment.StartUtc)];
            target.Put(data);
        }, options, cancellationToken);

        if (!updated.Succeeded)
        {
            errors.Add(S["The shift could not be changed: {0}", string.Join(" ", updated.Errors.Select(error => error.Message))]);

            return null;
        }

        var period = string.IsNullOrEmpty(data.ScheduleId) ? null : await _periodStore.FindByIdAsync(data.ScheduleId, cancellationToken);

        if (period is not null)
        {
            // Keep the schedule's draft in step, so the next publish keeps the segment, or doesn't bring it back.
            var date = DateOnly.FromDateTime(CalendarTime.ToLocal(calendarEvent.StartUtc, timeZoneId));
            var draft = await _draftStore.FindByShiftKeyAsync(period.ItemId, date, data.ShiftKey ?? calendarEvent.SourceId, cancellationToken);

            if (draft is not null)
            {
                var draftSegments = draft.Segments.Where(segment => !IsFrom(segment.Source, segment.SourceId, source, sourceId)).ToList();

                if (added is not null)
                {
                    draftSegments.Add(new DraftShiftSegment
                    {
                        ActivityCodeId = added.ActivityCodeId,
                        Start = CalendarTime.ToLocal(added.StartUtc, timeZoneId),
                        End = CalendarTime.ToLocal(added.EndUtc, timeZoneId),
                        Source = added.Source,
                        SourceId = added.SourceId,
                    });
                }

                draft.Segments = [.. draftSegments.OrderBy(segment => segment.Start)];
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
            Kind = ScheduleChangeKind.SegmentsChanged,
            BeforeStartUtc = calendarEvent.StartUtc,
            BeforeEndUtc = calendarEvent.EndUtc,
            AfterStartUtc = calendarEvent.StartUtc,
            AfterEndUtc = calendarEvent.EndUtc,
            InitiatedBy = ScheduleChangeInitiator.Employer,
            Reason = reason,
            Source = source,
            PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
            ChangedBy = actor,
            ChangedUtc = _clock.UtcNow,
        }, cancellationToken);

        return updated.Value ?? calendarEvent;
    }

    private static bool IsFrom(string segmentSource, string segmentSourceId, string source, string sourceId)
        => string.Equals(segmentSource, source, StringComparison.Ordinal) && string.Equals(segmentSourceId, sourceId, StringComparison.Ordinal);

    private async Task<CalendarEvent> MoveAsync(CalendarEvent calendarEvent, ShiftData data, StaffMember worker, ScheduleChangeKind kind, ScheduleChangeInitiator initiatedBy, string reason, string actor, List<string> errors, CancellationToken cancellationToken)
    {
        var previousWorkerId = data.WorkerId;
        var options = new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
        var updated = await _eventManager.UpdateAsync(calendarEvent, target =>
        {
            ShiftEvents.Assign(target, worker);

            // A segment another module placed, such as a class, belongs to the person it was placed for, not the shift.
            if (!string.Equals(previousWorkerId, worker.ItemId, StringComparison.Ordinal))
            {
                data.Segments = [.. data.Segments.Where(segment => string.IsNullOrEmpty(segment.Source))];
            }

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
                if (!string.Equals(draft.WorkerId, worker.ItemId, StringComparison.Ordinal))
                {
                    draft.Segments = [.. draft.Segments.Where(segment => string.IsNullOrEmpty(segment.Source))];
                }

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
