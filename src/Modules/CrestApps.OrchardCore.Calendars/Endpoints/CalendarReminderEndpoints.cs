using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Endpoints;

/// <summary>
/// The reminders the signed-in user hasn't dismissed yet, which every admin page shows, and dismissing one.
/// </summary>
internal static class CalendarReminderEndpoints
{
    // A reminder no one dismissed stops showing after this long.
    private static readonly TimeSpan _shownFor = TimeSpan.FromDays(2);

    public static IEndpointRouteBuilder MapCalendarReminderEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("Admin/api/calendars/reminders", ListAsync)
            .RequireAuthorization()
            .WithName("CalendarsReminders");

        routes.MapPost("Admin/api/calendars/reminders/{id}/dismiss", DismissAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithName("CalendarsDismissReminder");

        return routes;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        CalendarUserReminderStore store,
        IClock clock,
        ILocalClock localClock)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CalendarPermissions.AccessCalendars))
        {
            return Results.Forbid();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var reminders = await store.ListShownAsync(userId, clock.UtcNow - _shownFor, 20, httpContext.RequestAborted);

        var items = new List<object>(reminders.Count);

        foreach (var reminder in reminders)
        {
            // The start is written in the site time zone here, so an all-day event keeps its own date in a browser
            // that sits in another zone.
            var localStart = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(reminder.StartUtc, DateTimeKind.Utc));

            items.Add(new
            {
                id = reminder.ItemId,
                eventId = reminder.EventId,
                title = reminder.Title,
                location = reminder.Location,
                calendar = reminder.CalendarName,
                start = CalendarAdminEndpoints.FormatInstant(reminder.StartUtc),
                end = CalendarAdminEndpoints.FormatInstant(reminder.EndUtc),
                allDay = reminder.AllDay,
                startText = localStart.ToString(reminder.AllDay ? "D" : "g", CultureInfo.CurrentCulture),
                minutesBefore = reminder.MinutesBefore,
            });
        }

        return Results.Ok(items);
    }

    private static async Task<IResult> DismissAsync(
        string id,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        CalendarUserReminderStore store,
        IClock clock)
    {
        await antiforgery.ValidateRequestAsync(httpContext);

        var reminder = await store.FindAsync(id, httpContext.RequestAborted);
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Someone else's reminder is answered as missing, so its id gives nothing away.
        if (reminder is null || !string.Equals(reminder.UserId, userId, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        await store.DismissAsync(reminder, clock.UtcNow, httpContext.RequestAborted);

        return Results.NoContent();
    }
}
