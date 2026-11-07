using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Takes the overlapping shifts off the schedule when a worker's time off is approved, for teams set to remove them:
/// the event is canceled, the schedule's draft loses it so the next publish doesn't put it back, and the change is
/// logged as the worker's. Other teams' shifts are left flagged on the request. When the time off is canceled, the
/// shifts still to come are put back, with their drafts, unless the worker has another shift then.
/// </summary>
internal sealed class TimeOffShiftRemover : ITimeOffApprovalHandler
{
    private readonly TeamPlanningPolicy _policy;
    private readonly ICalendarEventManager _eventManager;
    private readonly SchedulePeriodStore _periodStore;
    private readonly DraftShiftStore _draftStore;
    private readonly ScheduleChangeRecorder _changeRecorder;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly WorkerShiftReader _shiftReader;
    private readonly WorkerStore _workerStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeOffShiftRemover(
        TeamPlanningPolicy policy,
        ICalendarEventManager eventManager,
        SchedulePeriodStore periodStore,
        DraftShiftStore draftStore,
        ScheduleChangeRecorder changeRecorder,
        TeamTimeZoneResolver timeZones,
        WorkerShiftReader shiftReader,
        WorkerStore workerStore,
        IClock clock,
        IStringLocalizer<TimeOffShiftRemover> stringLocalizer)
    {
        _policy = policy;
        _eventManager = eventManager;
        _periodStore = periodStore;
        _draftStore = draftStore;
        _changeRecorder = changeRecorder;
        _timeZones = timeZones;
        _shiftReader = shiftReader;
        _workerStore = workerStore;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task ApprovedAsync(TimeOffApprovedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var shift in context.OverlappingShifts.ToArray())
        {
            if (!ShiftEvents.TryRead(shift, out var data) || await _policy.TimeOffShiftHandlingAsync(data.TeamId, cancellationToken) != TimeOffShiftHandling.Remove)
            {
                continue;
            }

            var canceled = await _eventManager.CancelAsync(shift, new CalendarChangeOptions { Actor = context.Actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true }, cancellationToken);

            if (!canceled.Succeeded)
            {
                continue;
            }

            var period = string.IsNullOrEmpty(data.ScheduleId) ? null : await _periodStore.FindByIdAsync(data.ScheduleId, cancellationToken);

            if (period is not null)
            {
                var timeZoneId = await _timeZones.GetAsync(data.TeamId, cancellationToken);
                var date = DateOnly.FromDateTime(CalendarTime.ToLocal(shift.StartUtc, timeZoneId));
                var draft = await _draftStore.FindByShiftKeyAsync(period.ItemId, date, data.ShiftKey ?? shift.SourceId, cancellationToken);

                if (draft is not null)
                {
                    _draftStore.Delete(draft);
                }

                context.Request.RemovedShifts.Add(new TimeOffRemovedShift { EventId = shift.ItemId, Draft = draft });
            }
            else
            {
                context.Request.RemovedShifts.Add(new TimeOffRemovedShift { EventId = shift.ItemId });
            }

            await _changeRecorder.RecordAsync(new ScheduleChange
            {
                ItemId = UniqueId.GenerateId(),
                PeriodId = data.ScheduleId,
                TeamId = data.TeamId,
                WorkerId = data.WorkerId,
                ShiftKey = data.ShiftKey ?? shift.SourceId,
                Kind = ScheduleChangeKind.Removed,
                BeforeStartUtc = shift.StartUtc,
                BeforeEndUtc = shift.EndUtc,
                InitiatedBy = ScheduleChangeInitiator.Worker,
                Reason = S["Approved time off"],
                Source = ScheduleChangeSources.TimeOff,
                PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
                ChangedBy = context.Actor,
                ChangedUtc = _clock.UtcNow,
            }, cancellationToken);

            context.OverlappingShifts.Remove(shift);
        }
    }

    public async Task CanceledAsync(TimeOffRequest request, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var worker = request.RemovedShifts.Count == 0 ? null : await _workerStore.FindByIdAsync(request.WorkerId, cancellationToken);

        if (worker is null)
        {
            return;
        }

        foreach (var removed in request.RemovedShifts.ToArray())
        {
            var shift = string.IsNullOrEmpty(removed.EventId) ? null : await _eventManager.FindByIdAsync(removed.EventId, cancellationToken);

            // Only shifts still to come, taken off and not since replaced, come back.
            if (shift is null || shift.Status != CalendarEventStatus.Cancelled || shift.StartUtc <= now || !shift.TryGet<ShiftData>(out var data))
            {
                continue;
            }

            if ((await _shiftReader.ListAsync(worker, shift.StartUtc, shift.EndUtc, cancellationToken)).Any(other => other.ItemId != shift.ItemId && other.StartUtc < shift.EndUtc && other.EndUtc > shift.StartUtc))
            {
                continue;
            }

            var restored = await _eventManager.UpdateAsync(shift, target => target.Status = CalendarEventStatus.Confirmed, new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true }, cancellationToken);

            if (!restored.Succeeded)
            {
                continue;
            }

            var period = string.IsNullOrEmpty(data.ScheduleId) ? null : await _periodStore.FindByIdAsync(data.ScheduleId, cancellationToken);

            // The draft comes back too, or the next publish would take the shift off again.
            if (period is not null && removed.Draft is { } draft && await _draftStore.FindByShiftKeyAsync(period.ItemId, draft.Date, draft.ShiftKey, cancellationToken) is null)
            {
                await _draftStore.CreateAsync(draft, cancellationToken);
            }

            await _changeRecorder.RecordAsync(new ScheduleChange
            {
                ItemId = UniqueId.GenerateId(),
                PeriodId = data.ScheduleId,
                TeamId = data.TeamId,
                WorkerId = data.WorkerId,
                ShiftKey = data.ShiftKey ?? shift.SourceId,
                Kind = ScheduleChangeKind.Added,
                AfterStartUtc = shift.StartUtc,
                AfterEndUtc = shift.EndUtc,
                InitiatedBy = ScheduleChangeInitiator.Worker,
                Reason = S["Time off canceled"],
                Source = ScheduleChangeSources.TimeOff,
                PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
                ChangedBy = actor,
                ChangedUtc = now,
            }, cancellationToken);

            request.RemovedShifts.Remove(removed);
        }
    }
}
