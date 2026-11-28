using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Gets one calendar the person can read, with its sharing, feed address and subscription state when they manage it.
/// </summary>
internal sealed class GetCalendarTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "getCalendar";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "calendarId": { "type": "string", "description": "The calendar's ID, from listCalendars." }
      },
      "required": ["calendarId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Gets one calendar: type, owner, time zone, visibility, enabled, overlap limit, room or office details, its event types, who it is shared with, whether it has a private feed address, and its subscription state and last sync error. Sharing, the feed address and the subscription are shown only to someone who manages the calendar; the feed address and the subscription address themselves are never shown. Use it before changing a calendar.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (calendar, level) = await CalendarToolSupport.FindCalendarAsync(services, user, Read(arguments, "calendarId"), cancellationToken);

        if (calendar is null)
        {
            return CalendarToolSupport.CalendarNotFound;
        }

        if (level < CalendarAccessLevel.Read)
        {
            return "The user can only see when that calendar is busy, not its details.";
        }

        return CalendarToolSupport.Json(await CalendarToolResults.DescribeCalendarAsync(services, calendar, level));
    }
}
