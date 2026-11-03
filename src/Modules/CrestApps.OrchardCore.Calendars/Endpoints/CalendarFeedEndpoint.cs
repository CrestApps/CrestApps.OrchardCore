using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Endpoints;

/// <summary>
/// The private iCalendar feed of a calendar: anyone with the secret address can subscribe to it, so it can be added to
/// Google Calendar, Outlook or a phone. Private events show as busy without their details.
/// </summary>
internal static class CalendarFeedEndpoint
{
    private const int MaxEvents = 5000;
    private const int PageSize = 500;

    public static IEndpointRouteBuilder MapCalendarFeedEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("calendars/feed/{token}.ics", GetFeedAsync)
            .AllowAnonymous()
            .WithName("CalendarsFeed");

        return routes;
    }

    private static async Task<IResult> GetFeedAsync(
        string token,
        ICalendarStore calendarStore,
        ICalendarEventStore eventStore,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
        {
            return Results.NotFound();
        }

        var calendar = await calendarStore.FindByFeedTokenAsync(token);

        if (calendar is null || !calendar.Enabled)
        {
            return Results.NotFound();
        }

        var events = new List<CalendarEvent>();

        for (var skip = 0; skip < MaxEvents; skip += PageSize)
        {
            var page = await eventStore.ListByCalendarAsync(calendar.ItemId, skip, PageSize);

            events.AddRange(page.Select(Redact));

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var text = ICalendarWriter.Write(calendar.Name, events, clock.UtcNow);

        return Results.Text(text, "text/calendar; charset=utf-8");
    }

    // A private event shows as busy time in the feed.
    private static CalendarEvent Redact(CalendarEvent calendarEvent)
    {
        if (calendarEvent.Visibility != CalendarEventVisibility.Private)
        {
            return calendarEvent;
        }

        return new CalendarEvent
        {
            ItemId = calendarEvent.ItemId,
            Uid = calendarEvent.Uid,
            Title = "Busy",
            AllDay = calendarEvent.AllDay,
            Start = calendarEvent.Start,
            End = calendarEvent.End,
            StartUtc = calendarEvent.StartUtc,
            EndUtc = calendarEvent.EndUtc,
            TimeZoneId = calendarEvent.TimeZoneId,
            Recurrence = calendarEvent.Recurrence,
            SeriesId = calendarEvent.SeriesId,
            OriginalStartUtc = calendarEvent.OriginalStartUtc,
            Status = calendarEvent.Status,
            Transparency = calendarEvent.Transparency,
            Visibility = CalendarEventVisibility.Private,
            Sequence = calendarEvent.Sequence,
        };
    }
}
