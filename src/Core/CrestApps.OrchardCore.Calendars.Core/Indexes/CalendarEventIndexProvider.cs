using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Maps <see cref="CalendarEvent"/> documents to <see cref="CalendarEventIndex"/> and <see cref="CalendarEventAttendeeIndex"/> rows.
/// </summary>
public sealed class CalendarEventIndexProvider : IndexProvider<CalendarEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventIndexProvider"/> class.
    /// </summary>
    public CalendarEventIndexProvider()
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CalendarEvent> context)
    {
        context.For<CalendarEventIndex>()
            .Map(calendarEvent => new CalendarEventIndex
            {
                ItemId = calendarEvent.ItemId,
                CalendarId = calendarEvent.CalendarId,
                EventType = calendarEvent.EventType,
                Status = calendarEvent.Status,
                Transparency = calendarEvent.Transparency,
                StartUtc = calendarEvent.StartUtc,
                EndUtc = calendarEvent.EndUtc,
                RangeEndUtc = GetRangeEnd(calendarEvent),
                IsRecurring = calendarEvent.Recurrence is not null && !string.IsNullOrWhiteSpace(calendarEvent.Recurrence.Rule),
                SeriesId = calendarEvent.SeriesId,
                OriginalStartUtc = calendarEvent.OriginalStartUtc,
                Source = calendarEvent.Source,
                SourceId = calendarEvent.SourceId,
                Uid = CalendarIndexProvider.Truncate(calendarEvent.Uid, 255),
            });

        context.For<CalendarEventAttendeeIndex>()
            .Map(calendarEvent => calendarEvent.Attendees
                .Where(attendee => !string.IsNullOrEmpty(attendee.UserId) || !string.IsNullOrEmpty(attendee.ResourceCalendarId) || !string.IsNullOrWhiteSpace(attendee.Email))
                .Select(attendee => new CalendarEventAttendeeIndex
                {
                    EventId = calendarEvent.ItemId,
                    CalendarId = calendarEvent.CalendarId,
                    UserId = attendee.UserId,
                    ResourceCalendarId = attendee.ResourceCalendarId,
                    Email = CalendarIndexProvider.Truncate(attendee.Email?.Trim().ToLowerInvariant(), 255),
                    Response = attendee.Response,
                    Status = calendarEvent.Status,
                    StartUtc = calendarEvent.StartUtc,
                    RangeEndUtc = GetRangeEnd(calendarEvent),
                }));
    }

    internal static DateTime GetRangeEnd(CalendarEvent calendarEvent)
    {
        var end = CalendarEventExpander.GetRangeEndUtc(calendarEvent);

        return end > CalendarEventIndex.Forever ? CalendarEventIndex.Forever : end;
    }
}
