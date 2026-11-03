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

    /// <summary>
    /// The schedule value that shows everyone's schedule.
    /// </summary>
    internal const string EveryoneSchedule = "everyone";

    private static async Task<IResult> GetOccurrencesAsync(
        string start,
        string end,
        string calendars,
        string schedule,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService,
        ICalendarQueryService queryService,
        ICalendarCategoryService categoryService,
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

        var S = localizerFactory.Create(typeof(CalendarAdminEndpoints));
        var mapper = new CalendarViewEventMapper(httpContext, typeManager, localizerFactory, linkGenerator, S);

        if (!string.IsNullOrWhiteSpace(schedule))
        {
            return await GetScheduleAsync(schedule.Trim(), fromUtc, toUtc, httpContext, authorizationService, accessService, calendarService, queryService, categoryService, mapper);
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

        await mapper.LoadCategoriesAsync(occurrences, categoryService, httpContext.RequestAborted);

        var result = new List<CalendarViewEvent>(occurrences.Count);

        foreach (var occurrence in occurrences)
        {
            accessible.TryGetValue(occurrence.Event.CalendarId ?? string.Empty, out var access);
            result.Add(mapper.Map(occurrence, access));
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// Reads user ids separated by commas, without blanks or repeats.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The user ids.</returns>
    internal static IReadOnlyList<string> ParseUserIds(string value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Take(50)
            .ToList();
    }

    // People's schedules (the events on the calendars they own and the events they attend), or everyone's. Seeing one needs
    // ListCalendarEvents for that schedule; an event's details need ViewCalendarEvents for that event, or it shows as busy.
    private static async Task<IResult> GetScheduleAsync(
        string schedule,
        DateTime fromUtc,
        DateTime toUtc,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService,
        ICalendarQueryService queryService,
        ICalendarCategoryService categoryService,
        CalendarViewEventMapper mapper)
    {
        var user = httpContext.User;
        var everyone = string.Equals(schedule, EveryoneSchedule, StringComparison.OrdinalIgnoreCase);
        var userIds = everyone ? [] : ParseUserIds(schedule);

        if (!everyone && userIds.Count == 0)
        {
            return Results.BadRequest();
        }

        foreach (var target in everyone ? [CalendarSchedule.Everyone] : userIds.Select(id => new CalendarSchedule(id)))
        {
            if (!await authorizationService.AuthorizeAsync(user, CalendarPermissions.ListCalendarEvents, target))
            {
                return Results.Forbid();
            }
        }

        var query = new CalendarOccurrenceQuery { FromUtc = fromUtc, ToUtc = toUtc };

        if (everyone)
        {
            query.AllCalendars = true;
        }
        else
        {
            var owned = await calendarService.ListAsync(new CalendarListQuery { OwnerIds = [.. userIds], Limit = 500 });

            query.CalendarIds = owned.Where(calendar => calendar.OwnerKind == CalendarOwnerKind.User).Select(calendar => calendar.ItemId).ToList();
            query.AttendeeUserIds = [.. userIds];
        }

        var occurrences = await queryService.GetOccurrencesAsync(query);
        var calendarIds = occurrences.Select(occurrence => occurrence.Event.CalendarId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        var accessByCalendar = new Dictionary<string, CalendarAccess>(StringComparer.Ordinal);

        foreach (var calendar in calendarIds.Count > 0 ? await calendarService.GetAsync(calendarIds) : [])
        {
            accessByCalendar[calendar.ItemId] = new CalendarAccess(calendar, await accessService.GetAccessLevelAsync(user, calendar));
        }

        var canView = new Dictionary<string, bool>(StringComparer.Ordinal);
        await mapper.LoadCategoriesAsync(occurrences, categoryService, httpContext.RequestAborted);

        var result = new List<CalendarViewEvent>(occurrences.Count);

        foreach (var occurrence in occurrences)
        {
            var calendarEvent = occurrence.Event;

            if (!canView.TryGetValue(calendarEvent.ItemId, out var visible))
            {
                visible = await authorizationService.AuthorizeAsync(user, CalendarPermissions.ViewCalendarEvents, calendarEvent);
                canView[calendarEvent.ItemId] = visible;
            }

            occurrence.BusyOnly = !visible;
            accessByCalendar.TryGetValue(calendarEvent.CalendarId ?? string.Empty, out var access);
            result.Add(mapper.Map(occurrence, access));
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
