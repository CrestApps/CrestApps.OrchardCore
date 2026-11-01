using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// What approving and canceling time off does: the Time off event on the team calendar, the ledger entries, and the
/// published shifts it overlaps.
/// </summary>
internal sealed class TimeOffApprovals
{
    private readonly TeamStore _teamStore;
    private readonly TimeOffLedgerStore _ledgerStore;
    private readonly WorkforceCalendarService _calendarService;
    private readonly ICalendarService _calendars;
    private readonly ICalendarEventManager _eventManager;
    private readonly WorkerShiftReader _shiftReader;
    private readonly IEnumerable<ITimeOffApprovalHandler> _handlers;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeOffApprovals(
        TeamStore teamStore,
        TimeOffLedgerStore ledgerStore,
        WorkforceCalendarService calendarService,
        ICalendarService calendars,
        ICalendarEventManager eventManager,
        WorkerShiftReader shiftReader,
        IEnumerable<ITimeOffApprovalHandler> handlers,
        IClock clock,
        IStringLocalizer<TimeOffApprovals> stringLocalizer)
    {
        _teamStore = teamStore;
        _ledgerStore = ledgerStore;
        _calendarService = calendarService;
        _calendars = calendars;
        _eventManager = eventManager;
        _shiftReader = shiftReader;
        _handlers = handlers;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Approves a request: the event, the Taken entry and the overlapping shifts. The caller saves the request.
    /// </summary>
    public async Task<IReadOnlyList<string>> ApproveAsync(TimeOffRequest request, WorkerProfile worker, TimeOffType type, string actor, bool automatic, string note, CancellationToken cancellationToken = default)
    {
        var team = await _teamStore.FindByIdAsync(request.TeamId, cancellationToken);

        if (team is null)
        {
            return [S["The team doesn't exist."]];
        }

        await _calendarService.EnsureCalendarAsync(team, cancellationToken);

        var calendar = await _calendars.FindByIdAsync(team.CalendarId, cancellationToken);
        var calendarEvent = await _eventManager.NewAsync(calendar, WorkforceConstants.EventTypes.TimeOff, cancellationToken: cancellationToken);

        ShiftEvents.Assign(calendarEvent, worker);
        calendarEvent.Title = S["{0}: {1}", worker.DisplayName, type.Name];
        calendarEvent.TimeZoneId = request.TimeZoneId;
        calendarEvent.AllDay = !request.IsPartialDay;
        calendarEvent.Start = request.IsPartialDay ? request.StartDate.ToDateTime(request.StartTime.Value) : request.StartDate.ToDateTime(TimeOnly.MinValue);
        calendarEvent.End = request.IsPartialDay ? request.StartDate.ToDateTime(request.EndTime.Value) : request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        calendarEvent.Status = CalendarEventStatus.Confirmed;
        calendarEvent.Transparency = CalendarTransparency.Busy;
        calendarEvent.Color = type.Color;
        calendarEvent.Source = WorkforceConstants.Source;
        calendarEvent.SourceId = request.ItemId;
        calendarEvent.Editable = false;
        calendarEvent.Put(new TimeOffEventData { RequestId = request.ItemId, WorkerId = worker.ItemId, TypeId = type.ItemId });

        var created = await _eventManager.CreateAsync(calendarEvent, Options(actor), cancellationToken);

        if (!created.Succeeded)
        {
            return [S["The time off could not be put on the calendar: {0}", string.Join(" ", created.Errors.Select(error => error.Message))]];
        }

        calendarEvent = created.Value ?? calendarEvent;

        var now = _clock.UtcNow;

        if (!string.IsNullOrEmpty(request.PolicyId) && request.Amount > 0 && await _ledgerStore.FindByKeyAsync(TakenKey(request), cancellationToken) is null)
        {
            await _ledgerStore.CreateAsync(new TimeOffLedgerEntry
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = request.WorkerId,
                PolicyId = request.PolicyId,
                Kind = TimeOffLedgerKind.Taken,
                Amount = -request.Amount,
                EffectiveDate = request.StartDate,
                SourceRef = request.ItemId,
                IdempotencyKey = TakenKey(request),
                Actor = actor,
                CreatedUtc = now,
            }, cancellationToken);
        }

        request.Status = TimeOffRequestStatus.Approved;
        request.AutoApproved = automatic;
        request.DecidedBy = actor;
        request.DecidedUtc = now;
        request.DecisionNote = note;
        request.CalendarEventId = calendarEvent.ItemId;
        request.ModifiedUtc = now;

        var shifts = (await _shiftReader.ListAsync(worker, calendarEvent.StartUtc, calendarEvent.EndUtc, cancellationToken))
            .Where(shift => shift.StartUtc < calendarEvent.EndUtc && shift.EndUtc > calendarEvent.StartUtc)
            .ToList();
        var context = new TimeOffApprovedContext
        {
            Request = request,
            Worker = worker,
            OverlappingShifts = shifts,
            Actor = actor,
        };

        foreach (var handler in _handlers)
        {
            await handler.ApprovedAsync(context, cancellationToken);
        }

        request.FlaggedShiftEventIds = context.OverlappingShifts.Select(shift => shift.ItemId).ToList();

        return [];
    }

    /// <summary>
    /// Undoes approved time off: gives the balance back and cancels the event. The caller saves the request.
    /// </summary>
    public async Task UndoAsync(TimeOffRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        if (!string.IsNullOrEmpty(request.PolicyId)
            && await _ledgerStore.FindByKeyAsync(TakenKey(request), cancellationToken) is { } taken
            && await _ledgerStore.FindByKeyAsync("reversal:" + request.ItemId, cancellationToken) is null)
        {
            await _ledgerStore.CreateAsync(new TimeOffLedgerEntry
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = request.WorkerId,
                PolicyId = request.PolicyId,
                Kind = TimeOffLedgerKind.Reversal,
                Amount = -taken.Amount,
                EffectiveDate = taken.EffectiveDate,
                SourceRef = request.ItemId,
                IdempotencyKey = "reversal:" + request.ItemId,
                Actor = actor,
                CreatedUtc = now,
            }, cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.CalendarEventId)
            && await _eventManager.FindByIdAsync(request.CalendarEventId, cancellationToken) is { } calendarEvent
            && calendarEvent.Status != CalendarEventStatus.Cancelled)
        {
            await _eventManager.CancelAsync(calendarEvent, Options(actor), cancellationToken);
        }
    }

    private static string TakenKey(TimeOffRequest request)
    {
        return "taken:" + request.ItemId;
    }

    private static CalendarChangeOptions Options(string actor)
    {
        return new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
    }
}
