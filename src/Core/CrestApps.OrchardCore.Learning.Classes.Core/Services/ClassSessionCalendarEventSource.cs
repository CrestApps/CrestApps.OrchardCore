using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Shows a learner's class sessions on their own calendar: each meeting of a session they have a place in (confirmed or
/// attended), as an event that cannot be edited, read from the registrations rather than copied into events. A session
/// whose room has a calendar is also a stored event on the room's calendar; this source covers the learner's side,
/// with or without a room.
/// </summary>
internal sealed class ClassSessionCalendarEventSource : ICalendarEventSource
{
    /// <summary>
    /// The event type of a class meeting on a learner's calendar.
    /// </summary>
    public const string EventType = "LearningClassSession";

    private static readonly AppointmentStatus[] _places = [AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    private readonly IServiceProvider _services;

    // The calendar query service builds every event source, and the session service builds the calendar event manager,
    // so the services this source reads are resolved when it runs, not when it is built.
    public ClassSessionCalendarEventSource(IServiceProvider services)
    {
        _services = services;
    }

    public string Name => "LearningClassSessions";

    public async Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Only for the calendars asked for by id, which the caller has already checked access to.
        if (query.CalendarIds.Count == 0
            || (query.EventTypes.Count > 0 && !query.EventTypes.Contains(EventType))
            || (query.Sources.Count > 0 && !query.Sources.Contains(Name)))
        {
            return [];
        }

        var calendars = _services.GetRequiredService<ICalendarService>();
        var sessionService = _services.GetRequiredService<ISchedulingSessionService>();
        var occurrences = new List<CalendarOccurrence>();
        var sessions = new Dictionary<string, SchedulingSession>(StringComparer.Ordinal);

        foreach (var calendar in await calendars.GetAsync(query.CalendarIds, cancellationToken))
        {
            if (calendar.OwnerKind != CalendarOwnerKind.User || string.IsNullOrEmpty(calendar.OwnerId))
            {
                continue;
            }

            var places = (await sessionService.ListRegistrationsForUserAsync(calendar.OwnerId, cancellationToken))
                .Where(appointment => appointment.Source == ClassesConstants.RegistrationSource && !string.IsNullOrEmpty(appointment.SessionId) && _places.Contains(appointment.Status));

            foreach (var place in places)
            {
                if (!sessions.TryGetValue(place.SessionId, out var session))
                {
                    session = await sessionService.FindByIdAsync(place.SessionId, cancellationToken);
                    sessions[place.SessionId] = session;
                }

                if (session is null || session.Status == SchedulingSessionStatus.Cancelled)
                {
                    continue;
                }

                var zone = string.IsNullOrEmpty(calendar.TimeZoneId) ? "UTC" : calendar.TimeZoneId;

                foreach (var meeting in session.Meetings.Where(meeting => meeting.EndUtc > query.FromUtc && meeting.StartUtc < query.ToUtc))
                {
                    var start = CalendarTime.ToLocal(meeting.StartUtc, zone);
                    var end = CalendarTime.ToLocal(meeting.EndUtc, zone);
                    var calendarEvent = new CalendarEvent
                    {
                        ItemId = "class-" + place.ItemId + "-" + meeting.StartUtc.Ticks,
                        CalendarId = calendar.ItemId,
                        EventType = EventType,
                        Title = session.Title,
                        Location = string.IsNullOrWhiteSpace(session.Location) ? null : session.Location,
                        Start = start,
                        End = end,
                        TimeZoneId = zone,
                        StartUtc = meeting.StartUtc,
                        EndUtc = meeting.EndUtc,
                        Status = CalendarEventStatus.Confirmed,
                        Transparency = CalendarTransparency.Busy,
                        Source = Name,
                        SourceId = place.ItemId,
                        Editable = false,
                    };

                    occurrences.Add(new CalendarOccurrence
                    {
                        Event = calendarEvent,
                        SeriesId = calendarEvent.ItemId,
                        OriginalStartUtc = meeting.StartUtc,
                        Start = start,
                        End = end,
                        StartUtc = meeting.StartUtc,
                        EndUtc = meeting.EndUtc,
                    });
                }
            }
        }

        return occurrences;
    }
}
