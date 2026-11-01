using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reads a worker's published shifts on the calendars of every team they belong to.
/// </summary>
internal sealed class WorkerShiftReader
{
    private readonly TeamStore _teamStore;
    private readonly ICalendarQueryService _queryService;

    public WorkerShiftReader(TeamStore teamStore, ICalendarQueryService queryService)
    {
        _teamStore = teamStore;
        _queryService = queryService;
    }

    public Task<IReadOnlyList<CalendarEvent>> ListAsync(WorkerProfile worker, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return ListAsync(worker, fromUtc, toUtc, WorkforceConstants.EventTypes.Shift, calendarEvent => ShiftEvents.TryRead(calendarEvent, out var data) && data.WorkerId == worker.ItemId, cancellationToken);
    }

    /// <summary>
    /// Lists a worker's approved time off on the calendars of every team they belong to.
    /// </summary>
    public Task<IReadOnlyList<CalendarEvent>> ListTimeOffAsync(WorkerProfile worker, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return ListAsync(worker, fromUtc, toUtc, WorkforceConstants.EventTypes.TimeOff, calendarEvent => calendarEvent.Status != CalendarEventStatus.Cancelled
            && calendarEvent.TryGet<TimeOffEventData>(out var data)
            && data.WorkerId == worker.ItemId, cancellationToken);
    }

    private async Task<IReadOnlyList<CalendarEvent>> ListAsync(WorkerProfile worker, DateTime fromUtc, DateTime toUtc, string eventType, Func<CalendarEvent, bool> belongs, CancellationToken cancellationToken)
    {
        var teamIds = worker.TeamMemberships.Select(membership => membership.TeamId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (teamIds.Length == 0)
        {
            return [];
        }

        var calendarIds = (await _teamStore.GetAsync(teamIds, cancellationToken))
            .Select(team => team.CalendarId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (calendarIds.Length == 0)
        {
            return [];
        }

        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = calendarIds,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EventTypes = [eventType],
            Sources = [WorkforceConstants.Source],
        }, cancellationToken);

        return occurrences
            .Select(occurrence => occurrence.Event)
            .Where(belongs)
            .DistinctBy(calendarEvent => calendarEvent.ItemId)
            .OrderBy(calendarEvent => calendarEvent.StartUtc)
            .ToArray();
    }
}
