using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Teams.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Team members who are away now because a calendar says so: an event of a type that marks an absence (out of office,
/// time off) is happening now on their own calendar, or they attend it and haven't declined. Team assignment doesn't
/// push work to them while it lasts.
/// </summary>
internal sealed class CalendarAbsenceAvailabilitySource : ITeamMemberAvailabilitySource
{
    private readonly ICalendarService _calendars;
    private readonly ICalendarQueryService _queries;
    private readonly ICalendarTypeManager _types;
    private readonly IClock _clock;

    public CalendarAbsenceAvailabilitySource(ICalendarService calendars, ICalendarQueryService queries, ICalendarTypeManager types, IClock clock)
    {
        _calendars = calendars;
        _queries = queries;
        _types = types;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal) ?? [];

        if (ids.Count == 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var away = new HashSet<string>(StringComparer.Ordinal);

        // Their own calendars (a personal calendar's owner need not be an attendee of its events).
        var owned = (await _calendars.ListAsync(new CalendarListQuery { OwnerIds = [.. ids] }, cancellationToken))
            .Where(calendar => calendar.OwnerKind == CalendarOwnerKind.User && ids.Contains(calendar.OwnerId))
            .ToDictionary(calendar => calendar.ItemId, StringComparer.Ordinal);

        if (owned.Count > 0)
        {
            var ownedNow = await _queries.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = [.. owned.Keys],
                FromUtc = now,
                ToUtc = now.AddMinutes(1),
            }, cancellationToken);

            foreach (var occurrence in ownedNow)
            {
                if (owned.TryGetValue(occurrence.Event.CalendarId, out var calendar) && MarksAbsence(calendar, occurrence.Event))
                {
                    away.Add(calendar.OwnerId);
                }
            }
        }

        // Events on any calendar they attend, such as an out-of-office day on the team calendar.
        var attended = await _queries.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = [.. ids],
            FromUtc = now,
            ToUtc = now.AddMinutes(1),
        }, cancellationToken);

        if (attended.Count > 0)
        {
            var calendars = (await _calendars.GetAsync(attended.Select(occurrence => occurrence.Event.CalendarId).Distinct(StringComparer.Ordinal), cancellationToken))
                .ToDictionary(calendar => calendar.ItemId, StringComparer.Ordinal);

            foreach (var occurrence in attended)
            {
                if (!calendars.TryGetValue(occurrence.Event.CalendarId, out var calendar) || !MarksAbsence(calendar, occurrence.Event))
                {
                    continue;
                }

                foreach (var attendee in occurrence.Event.Attendees ?? [])
                {
                    if (attendee is not null && attendee.Response != CalendarAttendeeResponse.Declined && ids.Contains(attendee.UserId ?? string.Empty))
                    {
                        away.Add(attendee.UserId);
                    }
                }
            }
        }

        return away;
    }

    private bool MarksAbsence(Calendar calendar, CalendarEvent calendarEvent)
        => calendarEvent.Status != CalendarEventStatus.Cancelled
            && _types.FindEventType(calendar.Type, calendarEvent.EventType)?.MarksAbsence == true;
}
