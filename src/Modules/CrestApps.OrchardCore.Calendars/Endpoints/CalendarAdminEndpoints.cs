using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Endpoints;

/// <summary>
/// The JSON endpoints the calendar admin screens call: occurrences in a range, moving an occurrence, and the user search.
/// </summary>
internal static class CalendarAdminEndpoints
{
    public static IEndpointRouteBuilder MapCalendarAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("Admin/api/calendars/occurrences", GetOccurrencesAsync)
            .RequireAuthorization()
            .WithName("CalendarsOccurrences");

        routes.MapPost("Admin/api/calendars/occurrences/move", MoveAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithName("CalendarsMoveOccurrence");

        routes.MapGet("Admin/api/calendars/users/search", SearchUsersAsync)
            .RequireAuthorization()
            .WithName("CalendarsUserSearch");

        return routes;
    }

    private static async Task<IResult> GetOccurrencesAsync(
        string start,
        string end,
        string calendars,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarQueryService queryService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        LinkGenerator linkGenerator)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CalendarPermissions.AccessCalendars))
        {
            return Results.Forbid();
        }

        if (!TryParseInstant(start, out var fromUtc) || !TryParseInstant(end, out var toUtc) || toUtc <= fromUtc)
        {
            return Results.BadRequest();
        }

        var requested = (calendars ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var accessible = (await accessService.ListAccessibleAsync(httpContext.User, CalendarAccessLevel.FreeBusy))
            .Where(access => requested.Contains(access.Calendar.ItemId))
            .ToDictionary(access => access.Calendar.ItemId, StringComparer.Ordinal);

        if (accessible.Count == 0)
        {
            return Results.Ok(Array.Empty<CalendarViewEvent>());
        }

        var occurrences = await queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = accessible.Keys.ToList(),
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Viewer = httpContext.User,
            IncludeEventSources = true,
        });

        var S = localizerFactory.Create(typeof(CalendarAdminEndpoints));
        var result = new List<CalendarViewEvent>(occurrences.Count);

        foreach (var occurrence in occurrences)
        {
            var calendarEvent = occurrence.Event;
            accessible.TryGetValue(calendarEvent.CalendarId ?? string.Empty, out var access);

            var calendar = access?.Calendar;
            var eventType = calendar is null ? null : typeManager.FindEventType(calendar.Type, calendarEvent.EventType);
            var canChange = calendar is not null && !occurrence.BusyOnly && calendarEvent.Editable && !string.IsNullOrEmpty(calendarEvent.ItemId) && access.Level >= CalendarAccessLevel.Write;
            var editUrl = canChange
                ? linkGenerator.GetPathByAction(httpContext, "Edit", "CalendarEvents", new
                {
                    area = CalendarsConstants.Features.Calendars,
                    id = occurrence.IsOverride ? calendarEvent.ItemId : occurrence.SeriesId,
                    occurrence = occurrence.IsRecurring && !occurrence.IsOverride ? FormatInstant(occurrence.OriginalStartUtc) : null,
                })
                : null;

            result.Add(new CalendarViewEvent
            {
                Id = occurrence.GetOccurrenceId(),
                EventId = calendarEvent.ItemId,
                CalendarId = calendarEvent.CalendarId,
                Title = occurrence.BusyOnly ? S["Busy"].Value : calendarEvent.Title,
                AllDay = calendarEvent.AllDay,
                Start = calendarEvent.AllDay ? occurrence.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : FormatInstant(occurrence.StartUtc),
                End = calendarEvent.AllDay ? occurrence.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : FormatInstant(occurrence.EndUtc),
                Color = occurrence.BusyOnly ? "#868e96" : calendarEvent.Color ?? eventType?.Color ?? calendar?.Color,
                Editable = canChange,
                Details = new CalendarViewEventDetails
                {
                    BusyOnly = occurrence.BusyOnly,
                    Calendar = calendar?.Name,
                    EventType = occurrence.BusyOnly || eventType is null ? null : localizerFactory.Localize(eventType.DisplayName).Value,
                    Location = occurrence.BusyOnly ? null : calendarEvent.Location,
                    Description = occurrence.BusyOnly ? null : calendarEvent.Description,
                    Status = calendarEvent.Status switch
                    {
                        CalendarEventStatus.Tentative => S["Tentative"].Value,
                        CalendarEventStatus.Cancelled => S["Cancelled"].Value,
                        _ => S["Confirmed"].Value,
                    },
                    Recurring = occurrence.IsRecurring,
                    OriginalStart = occurrence.IsRecurring ? FormatInstant(occurrence.OriginalStartUtc) : null,
                    Source = calendarEvent.Source,
                    Attendees = occurrence.BusyOnly
                        ? []
                        : calendarEvent.Attendees.Select(attendee => attendee.Name ?? attendee.Email ?? attendee.ResourceCalendarId).Where(name => !string.IsNullOrEmpty(name)).ToList(),
                    EditUrl = editUrl,
                },
            });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> MoveAsync(
        CalendarMoveRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        CalendarEventEditor editor,
        IStringLocalizerFactory localizerFactory)
    {
        await antiforgery.ValidateRequestAsync(httpContext);

        var S = localizerFactory.Create(typeof(CalendarAdminEndpoints));
        var calendarEvent = await eventManager.FindByIdAsync(request?.EventId);

        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        var calendar = await calendarService.FindByIdAsync(calendarEvent.CalendarId);

        if (calendar is null || !await editor.CanChangeAsync(httpContext.User, calendarEvent, calendar))
        {
            return Results.Json(new { errors = new[] { S["You can't change this event."].Value } }, statusCode: StatusCodes.Status403Forbidden);
        }

        var timeZoneId = calendarEvent.TimeZoneId;
        var start = request.AllDay ? request.Start.Date : CalendarTime.ToLocal(request.Start, timeZoneId);
        var end = request.End.HasValue
            ? request.AllDay ? request.End.Value.Date : CalendarTime.ToLocal(request.End.Value, timeZoneId)
            : request.AllDay ? start.AddDays(1) : start.AddHours(1);

        void Move(CalendarEvent target)
        {
            target.AllDay = request.AllDay;
            target.Start = start;
            target.End = end;
        }

        var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(httpContext.User) };
        var result = request.OriginalStartUtc.HasValue && calendarEvent.SeriesId is null && calendarEvent.Recurrence is not null
            ? await eventManager.UpdateOccurrenceAsync(calendarEvent, DateTime.SpecifyKind(request.OriginalStartUtc.Value, DateTimeKind.Utc), request.Scope, Move, options)
            : await eventManager.UpdateAsync(calendarEvent, Move, options);

        if (!result.Succeeded)
        {
            return Results.Json(new { errors = result.Errors.Select(error => error.Message) }, statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(new { id = result.Value.ItemId });
    }

    private static async Task<IResult> SearchUsersAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        CalendarUserLookup userLookup)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CalendarPermissions.AccessCalendars))
        {
            return Results.Forbid();
        }

        var users = await userLookup.SearchAsync(query);

        return Results.Ok(users.Select(user => new { value = user.Value, text = user.Text }));
    }

    internal static string FormatInstant(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    internal static bool TryParseInstant(string value, out DateTime utc)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
        {
            utc = offset.UtcDateTime;

            return true;
        }

        utc = default;

        return false;
    }
}
