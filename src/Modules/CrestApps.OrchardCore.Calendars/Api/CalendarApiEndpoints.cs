using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;

namespace CrestApps.OrchardCore.Calendars.Api;

/// <summary>
/// The calendars HTTP API under <c>/api/calendars/v1</c>. Every call acts as the signed-in client and goes through the same
/// services, validation, access rules and conflict checks as the admin screens.
/// </summary>
internal static class CalendarApiEndpoints
{
    public const string RoutePrefix = "api/calendars/v1";

    public static IEndpointRouteBuilder MapCalendarApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags("Calendars");

        group.MapGet("calendars", ListCalendarsAsync).WithName("CalendarsApiListCalendars").WithSummary("Lists the calendars the caller can see.");
        group.MapGet("events", ListEventsAsync).WithName("CalendarsApiListEvents").WithSummary("Lists the occurrences on calendars between two times.");
        group.MapGet("events/{id}", GetEventAsync).WithName("CalendarsApiGetEvent").WithSummary("Gets an event.");
        group.MapPost("events", CreateEventAsync).WithName("CalendarsApiCreateEvent").WithSummary("Creates an event.");
        group.MapPut("events/{id}", UpdateEventAsync).WithName("CalendarsApiUpdateEvent").WithSummary("Changes an event, or occurrences of a repeating event.");
        group.MapPost("events/{id}/cancel", CancelEventAsync).WithName("CalendarsApiCancelEvent").WithSummary("Cancels an event, or occurrences of a repeating event.");
        group.MapDelete("events/{id}", DeleteEventAsync).WithName("CalendarsApiDeleteEvent").WithSummary("Deletes an event.");
        group.MapPost("events/{id}/respond", RespondAsync).WithName("CalendarsApiRespond").WithSummary("Records the caller's answer to an invitation.");
        group.MapPost("free-busy", FreeBusyAsync).WithName("CalendarsApiFreeBusy").WithSummary("Gets busy time for calendars and users.");
        group.MapPost("free-time", FreeTimeAsync).WithName("CalendarsApiFreeTime").WithSummary("Finds times when calendars and users are all free.");

        return routes;
    }

    private static async Task<IResult> ListCalendarsAsync(HttpContext httpContext, ICalendarAccessService accessService)
    {
        var calendars = await accessService.ListAccessibleAsync(httpContext.User, CalendarAccessLevel.FreeBusy);

        return Results.Ok(calendars.Select(access => new
        {
            id = access.Calendar.ItemId,
            name = access.Calendar.Name,
            type = access.Calendar.Type,
            color = access.Calendar.Color,
            timeZoneId = access.Calendar.TimeZoneId,
            ownerId = access.Calendar.OwnerId,
            visibility = access.Calendar.Visibility.ToString(),
            access = access.Level.ToString(),
        }));
    }

    private static async Task<IResult> ListEventsAsync(string calendarIds, string from, string to, HttpContext httpContext, ICalendarAccessService accessService, ICalendarQueryService queryService)
    {
        if (!DateTimeOffset.TryParse(from, out var fromValue) || !DateTimeOffset.TryParse(to, out var toValue) || toValue <= fromValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["from"] = ["Give from and to as ISO 8601 times, with to after from."] });
        }

        var requested = CalendarWorkflowService.Split(calendarIds);
        var accessible = (await accessService.ListAccessibleAsync(httpContext.User, CalendarAccessLevel.FreeBusy))
            .Where(access => requested.Count == 0 || requested.Contains(access.Calendar.ItemId))
            .Select(access => access.Calendar.ItemId)
            .ToList();

        var occurrences = await queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = accessible,
            FromUtc = fromValue.UtcDateTime,
            ToUtc = toValue.UtcDateTime,
            Viewer = httpContext.User,
        });

        return Results.Ok(occurrences.Select(occurrence => occurrence.BusyOnly
            ? new Dictionary<string, object>
            {
                ["CalendarId"] = occurrence.Event.CalendarId,
                ["Busy"] = true,
                ["OccurrenceStartUtc"] = CalendarWorkflowInput.Format(occurrence.StartUtc),
                ["OccurrenceEndUtc"] = CalendarWorkflowInput.Format(occurrence.EndUtc),
            }
            : WithOccurrence(occurrence)));
    }

    private static async Task<IResult> GetEventAsync(string id, HttpContext httpContext, ICalendarService calendarService, ICalendarEventManager eventManager, CalendarEventEditor editor)
    {
        var (calendarEvent, calendar) = await LoadAsync(id, calendarService, eventManager);

        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        return await editor.CanReadAsync(httpContext.User, calendarEvent, calendar)
            ? Results.Ok(CalendarWorkflowInput.ToDictionary(calendarEvent))
            : Results.Forbid();
    }

    private static async Task<IResult> CreateEventAsync(CalendarEventInput input, HttpContext httpContext, ICalendarService calendarService, ICalendarEventManager eventManager, CalendarEventEditor editor)
    {
        var calendar = await calendarService.FindByIdAsync(input?.CalendarId);

        if (calendar is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["calendarId"] = ["The calendar doesn't exist."] });
        }

        if (!await editor.CanAddAsync(httpContext.User, calendar))
        {
            return Results.Forbid();
        }

        var calendarEvent = await eventManager.NewAsync(calendar, input.EventType);
        var errors = new List<string>();

        CalendarEventInputApplier.Apply(input, calendarEvent, errors);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [string.Empty] = errors.ToArray() });
        }

        var result = await eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = GetUserId(httpContext.User) });

        return ToResult(result, created: true);
    }

    private static async Task<IResult> UpdateEventAsync(string id, string occurrence, string scope, CalendarEventInput input, HttpContext httpContext, ICalendarService calendarService, ICalendarEventManager eventManager, CalendarEventEditor editor)
    {
        var (calendarEvent, calendar) = await LoadAsync(id, calendarService, eventManager);

        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        if (!await editor.CanChangeAsync(httpContext.User, calendarEvent, calendar))
        {
            return Results.Forbid();
        }

        var errors = new List<string>();
        var options = new CalendarChangeOptions { Actor = GetUserId(httpContext.User) };
        var result = DateTimeOffset.TryParse(occurrence, out var original)
            ? await eventManager.UpdateOccurrenceAsync(calendarEvent, original.UtcDateTime, ParseScope(scope), target => CalendarEventInputApplier.Apply(input ?? new CalendarEventInput(), target, errors), options)
            : await eventManager.UpdateAsync(calendarEvent, target => CalendarEventInputApplier.Apply(input ?? new CalendarEventInput(), target, errors), options);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [string.Empty] = errors.ToArray() });
        }

        return ToResult(result, created: false);
    }

    private static async Task<IResult> CancelEventAsync(string id, string occurrence, string scope, HttpContext httpContext, ICalendarService calendarService, ICalendarEventManager eventManager, CalendarEventEditor editor)
    {
        var (calendarEvent, calendar) = await LoadAsync(id, calendarService, eventManager);

        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        if (!await editor.CanChangeAsync(httpContext.User, calendarEvent, calendar))
        {
            return Results.Forbid();
        }

        var options = new CalendarChangeOptions { Actor = GetUserId(httpContext.User) };
        var result = DateTimeOffset.TryParse(occurrence, out var original)
            ? await eventManager.CancelOccurrenceAsync(calendarEvent, original.UtcDateTime, ParseScope(scope), options)
            : await eventManager.CancelAsync(calendarEvent, options);

        return ToResult(result, created: false);
    }

    private static async Task<IResult> DeleteEventAsync(string id, HttpContext httpContext, ICalendarService calendarService, ICalendarEventManager eventManager, CalendarEventEditor editor)
    {
        var (calendarEvent, calendar) = await LoadAsync(id, calendarService, eventManager);

        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        if (!await editor.CanChangeAsync(httpContext.User, calendarEvent, calendar))
        {
            return Results.Forbid();
        }

        await eventManager.DeleteAsync(calendarEvent, new CalendarChangeOptions { Actor = GetUserId(httpContext.User) });

        return Results.NoContent();
    }

    private static async Task<IResult> RespondAsync(string id, CalendarResponseInput input, HttpContext httpContext, ICalendarEventManager eventManager)
    {
        var calendarEvent = await eventManager.FindByIdAsync(id);
        var userId = GetUserId(httpContext.User);

        if (calendarEvent is null || string.IsNullOrEmpty(userId))
        {
            return Results.NotFound();
        }

        if (!Enum.TryParse<CalendarAttendeeResponse>(input?.Response, ignoreCase: true, out var response))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["response"] = ["Use Accepted, Tentative or Declined."] });
        }

        return ToResult(await eventManager.RespondAsync(calendarEvent, "user:" + userId, response, new CalendarChangeOptions { Actor = userId }), created: false);
    }

    private static async Task<IResult> FreeBusyAsync(CalendarFreeBusyInput input, HttpContext httpContext, IAuthorizationService authorizationService, ICalendarService calendarService, ICalendarAccessService accessService, ICalendarAvailabilityService availabilityService)
    {
        var query = await BuildQueryAsync(input, httpContext, authorizationService, calendarService, accessService);

        if (query is null)
        {
            return Results.Forbid();
        }

        var busy = await availabilityService.GetBusyAsync(query);

        return Results.Ok(busy.Select(interval => new
        {
            startUtc = CalendarWorkflowInput.Format(interval.StartUtc),
            endUtc = CalendarWorkflowInput.Format(interval.EndUtc),
            calendarId = interval.CalendarId,
            userId = interval.UserId,
            tentative = interval.Tentative,
        }));
    }

    private static async Task<IResult> FreeTimeAsync(CalendarFreeBusyInput input, HttpContext httpContext, IAuthorizationService authorizationService, ICalendarService calendarService, ICalendarAccessService accessService, ICalendarAvailabilityService availabilityService)
    {
        var query = await BuildQueryAsync(input, httpContext, authorizationService, calendarService, accessService);

        if (query is null)
        {
            return Results.Forbid();
        }

        var windows = await availabilityService.FindFreeWindowsAsync(query, TimeSpan.FromMinutes(Math.Clamp(input.DurationMinutes, 5, 1440)), maxResults: 50);

        return Results.Ok(windows.Select(window => new
        {
            startUtc = CalendarWorkflowInput.Format(window.StartUtc),
            endUtc = CalendarWorkflowInput.Format(window.EndUtc),
        }));
    }

    private static async Task<CalendarBusyQuery> BuildQueryAsync(CalendarFreeBusyInput input, HttpContext httpContext, IAuthorizationService authorizationService, ICalendarService calendarService, ICalendarAccessService accessService)
    {
        if (input is null || !await authorizationService.AuthorizeAsync(httpContext.User, CalendarPermissions.AccessCalendars))
        {
            return null;
        }

        var calendars = await calendarService.GetAsync(input.CalendarIds ?? []);

        foreach (var calendar in calendars)
        {
            if (await accessService.GetAccessLevelAsync(httpContext.User, calendar) < CalendarAccessLevel.FreeBusy)
            {
                return null;
            }
        }

        return new CalendarBusyQuery
        {
            CalendarIds = calendars.Select(calendar => calendar.ItemId).ToList(),
            UserIds = (input.UserIds ?? []).Take(50).ToList(),
            FromUtc = input.From.UtcDateTime,
            ToUtc = input.To.UtcDateTime > input.From.UtcDateTime.AddDays(62) ? input.From.UtcDateTime.AddDays(62) : input.To.UtcDateTime,
        };
    }

    private static async Task<(CalendarEvent Event, Calendar Calendar)> LoadAsync(string id, ICalendarService calendarService, ICalendarEventManager eventManager)
    {
        var calendarEvent = await eventManager.FindByIdAsync(id);
        var calendar = calendarEvent is null ? null : await calendarService.FindByIdAsync(calendarEvent.CalendarId);

        return calendar is null ? (null, null) : (calendarEvent, calendar);
    }

    private static IResult ToResult(CalendarResult<CalendarEvent> result, bool created)
    {
        if (result.Succeeded)
        {
            var body = CalendarWorkflowInput.ToDictionary(result.Value);

            return created ? Results.Created($"/{RoutePrefix}/events/{result.Value.ItemId}", body) : Results.Ok(body);
        }

        if (result.Errors.Any(error => error.Code == CalendarError.ConflictCode))
        {
            return Results.Problem(string.Join(" ", result.Errors.Select(error => error.Message)), statusCode: StatusCodes.Status409Conflict, title: "Conflict");
        }

        return Results.ValidationProblem(result.Errors.GroupBy(error => error.Field ?? string.Empty).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()));
    }

    private static Dictionary<string, object> WithOccurrence(CalendarOccurrence occurrence)
    {
        var item = CalendarWorkflowInput.ToDictionary(occurrence.Event);

        item["OccurrenceStartUtc"] = CalendarWorkflowInput.Format(occurrence.StartUtc);
        item["OccurrenceEndUtc"] = CalendarWorkflowInput.Format(occurrence.EndUtc);
        item["OriginalStartUtc"] = CalendarWorkflowInput.Format(occurrence.OriginalStartUtc);

        return item;
    }

    private static CalendarEditScope ParseScope(string scope)
    {
        return Enum.TryParse<CalendarEditScope>(scope, ignoreCase: true, out var value) ? value : CalendarEditScope.ThisOccurrence;
    }

    private static string GetUserId(ClaimsPrincipal user)
    {
        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
