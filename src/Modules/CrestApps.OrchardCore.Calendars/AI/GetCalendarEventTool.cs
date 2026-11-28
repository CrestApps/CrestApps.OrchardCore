using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Gets one event in full, when the person may see its details.
/// </summary>
internal sealed class GetCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "getCalendarEvent";

    /// <summary>
    /// The answer when an event does not exist or the caller may not see it.
    /// </summary>
    public const string NotFound = "There is no event with that ID whose details the user can see. Use findCalendarEvents to find it.";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "eventId": { "type": "string", "description": "The event's ID, from findCalendarEvents." }
      },
      "required": ["eventId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Gets one event in full: type, times and time zone, recurrence, status, visibility, category, reminders, each attendee with their answer, and whether the user can change it. Times are in the event's own time zone. Use it before updateCalendarEvent.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (calendarEvent, calendar) = await LoadAsync(services, Read(arguments, "eventId"), cancellationToken);

        // The same check the calendar view makes before it shows an event's details.
        if (calendarEvent is null || !await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, CalendarPermissions.ViewCalendarEvents, calendarEvent))
        {
            return NotFound;
        }

        return CalendarToolSupport.Json(await CalendarToolResults.DescribeEventAsync(services, user, calendarEvent, calendar));
    }

    /// <summary>
    /// Finds an event and its calendar.
    /// </summary>
    /// <returns>The event and its calendar, or <see langword="null"/>s when either is missing.</returns>
    internal static async Task<(CalendarEvent Event, Calendar Calendar)> LoadAsync(IServiceProvider services, string eventId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return (null, null);
        }

        var calendarEvent = await services.GetRequiredService<ICalendarEventManager>().FindByIdAsync(eventId.Trim(), cancellationToken);
        var calendar = calendarEvent is null ? null : await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarEvent.CalendarId, cancellationToken);

        return calendar is null ? (null, null) : (calendarEvent, calendar);
    }
}
