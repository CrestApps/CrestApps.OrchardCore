using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Shows time-off requests that wait for a place or a decision on their team's calendar, as tentative events that
/// can't be edited and don't make anyone busy. Approved time off is a stored event of its own.
/// </summary>
internal sealed class PendingTimeOffEventSource : ICalendarEventSource
{
    private static readonly TimeOffRequestStatus[] _open = [TimeOffRequestStatus.Pending, TimeOffRequestStatus.Waitlisted];

    private readonly TeamStore _teamStore;
    private readonly TimeOffRequestStore _requestStore;
    private readonly TimeOffTypeStore _typeStore;
    private readonly WorkerStore _workerStore;

    internal readonly IStringLocalizer S;

    public PendingTimeOffEventSource(
        TeamStore teamStore,
        TimeOffRequestStore requestStore,
        TimeOffTypeStore typeStore,
        WorkerStore workerStore,
        IStringLocalizer<PendingTimeOffEventSource> stringLocalizer)
    {
        _teamStore = teamStore;
        _requestStore = requestStore;
        _typeStore = typeStore;
        _workerStore = workerStore;
        S = stringLocalizer;
    }

    public string Name => "WorkforceTimeOffRequests";

    public async Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Only for the calendars asked for by id, which the caller has already checked access to.
        if (query.CalendarIds.Count == 0
            || (query.EventTypes.Count > 0 && !query.EventTypes.Contains(WorkforceConstants.EventTypes.TimeOff))
            || (query.Sources.Count > 0 && !query.Sources.Contains(Name)))
        {
            return [];
        }

        var calendars = new HashSet<string>(query.CalendarIds, StringComparer.Ordinal);
        var teams = (await _teamStore.ListAsync(cancellationToken))
            .Where(team => !string.IsNullOrEmpty(team.CalendarId) && calendars.Contains(team.CalendarId))
            .ToDictionary(team => team.ItemId, StringComparer.Ordinal);

        if (teams.Count == 0)
        {
            return [];
        }

        // A day either side, since the requests' days are in their own time zones.
        var requests = await _requestStore.ListForTeamsAsync(
            teams.Keys.ToArray(),
            _open,
            DateOnly.FromDateTime(query.FromUtc).AddDays(-1),
            DateOnly.FromDateTime(query.ToUtc).AddDays(1),
            cancellationToken);

        if (requests.Count == 0)
        {
            return [];
        }

        var types = (await _typeStore.ListAsync(cancellationToken)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var workers = (await _workerStore.GetAsync(requests.Select(request => request.WorkerId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken))
            .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
        var occurrences = new List<CalendarOccurrence>();

        foreach (var request in requests)
        {
            var start = request.IsPartialDay ? request.StartDate.ToDateTime(request.StartTime.Value) : request.StartDate.ToDateTime(TimeOnly.MinValue);
            var end = request.IsPartialDay ? request.StartDate.ToDateTime(request.EndTime.Value) : request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var startUtc = CalendarTime.ToUtc(start, request.TimeZoneId);
            var endUtc = CalendarTime.ToUtc(end, request.TimeZoneId);

            if (endUtc <= query.FromUtc || startUtc >= query.ToUtc)
            {
                continue;
            }

            types.TryGetValue(request.TypeId ?? string.Empty, out var type);
            workers.TryGetValue(request.WorkerId ?? string.Empty, out var worker);

            var calendarEvent = new CalendarEvent
            {
                ItemId = "timeoff-" + request.ItemId,
                CalendarId = teams[request.TeamId].CalendarId,
                EventType = WorkforceConstants.EventTypes.TimeOff,
                Title = request.Status == TimeOffRequestStatus.Waitlisted
                    ? S["Waitlisted: {0}: {1}", worker?.DisplayName, type?.Name]
                    : S["Requested: {0}: {1}", worker?.DisplayName, type?.Name],
                AllDay = !request.IsPartialDay,
                Start = start,
                End = end,
                TimeZoneId = request.TimeZoneId,
                StartUtc = startUtc,
                EndUtc = endUtc,
                Status = CalendarEventStatus.Tentative,
                Transparency = CalendarTransparency.Free,
                Color = type?.Color,
                Source = Name,
                SourceId = request.ItemId,
                Editable = false,
            };

            occurrences.Add(new CalendarOccurrence
            {
                Event = calendarEvent,
                SeriesId = calendarEvent.ItemId,
                OriginalStartUtc = startUtc,
                Start = start,
                End = end,
                StartUtc = startUtc,
                EndUtc = endUtc,
            });
        }

        return occurrences;
    }
}
