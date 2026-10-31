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

    public async Task<IReadOnlyList<CalendarEvent>> ListAsync(WorkerProfile worker, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
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
            EventTypes = [WorkforceConstants.EventTypes.Shift],
            Sources = [WorkforceConstants.Source],
        }, cancellationToken);

        return occurrences
            .Select(occurrence => occurrence.Event)
            .Where(calendarEvent => ShiftEvents.TryRead(calendarEvent, out var data) && data.WorkerId == worker.ItemId)
            .DistinctBy(calendarEvent => calendarEvent.ItemId)
            .OrderBy(calendarEvent => calendarEvent.StartUtc)
            .ToArray();
    }
}
