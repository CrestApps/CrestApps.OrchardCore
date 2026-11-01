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
/// Takes the overlapping shifts off the schedule when a worker's time off is approved, for teams set to remove them:
/// the event is canceled, the schedule's draft loses it so the next publish doesn't put it back, and the change is
/// logged as the worker's. Other teams' shifts are left flagged on the request.
/// </summary>
internal sealed class TimeOffShiftRemover : ITimeOffApprovalHandler
{
    private readonly TeamPlanningPolicy _policy;
    private readonly ICalendarEventManager _eventManager;
    private readonly SchedulePeriodStore _periodStore;
    private readonly DraftShiftStore _draftStore;
    private readonly ScheduleChangeStore _changeStore;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeOffShiftRemover(
        TeamPlanningPolicy policy,
        ICalendarEventManager eventManager,
        SchedulePeriodStore periodStore,
        DraftShiftStore draftStore,
        ScheduleChangeStore changeStore,
        TeamTimeZoneResolver timeZones,
        IClock clock,
        IStringLocalizer<TimeOffShiftRemover> stringLocalizer)
    {
        _policy = policy;
        _eventManager = eventManager;
        _periodStore = periodStore;
        _draftStore = draftStore;
        _changeStore = changeStore;
        _timeZones = timeZones;
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
            }

            await _changeStore.CreateAsync(new ScheduleChange
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
                PublishedVersion = period?.PublishedVersion ?? data.PublishedVersion,
                ChangedBy = context.Actor,
                ChangedUtc = _clock.UtcNow,
            }, cancellationToken);

            context.OverlappingShifts.Remove(shift);
        }
    }
}
