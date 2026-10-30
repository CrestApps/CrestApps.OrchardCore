using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Cancels an event, or one occurrence of a repeating event.
/// </summary>
public sealed class CancelCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "cancelCalendarEvent";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "eventId": { "type": "string" },
        "occurrenceStart": { "type": "string", "description": "For a repeating event, the occurrenceStart findCalendarEvents returned. Only that occurrence is cancelled." }
      },
      "required": ["eventId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Cancels an event the user can change. Confirm with the user first.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var eventManager = services.GetRequiredService<ICalendarEventManager>();
        var calendarEvent = await eventManager.FindByIdAsync(Read(arguments, "eventId"), cancellationToken);
        var calendar = calendarEvent is null ? null : await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarEvent.CalendarId, cancellationToken);

        if (calendar is null || !await services.GetRequiredService<CalendarEventEditor>().CanChangeAsync(user, calendarEvent, calendar))
        {
            return "The user can't change that event.";
        }

        var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(user) };
        var result = DateTimeOffset.TryParse(Read(arguments, "occurrenceStart"), out var original)
            ? await eventManager.CancelOccurrenceAsync(calendarEvent, original.UtcDateTime, CalendarEditScope.ThisOccurrence, options)
            : await eventManager.CancelAsync(calendarEvent, options);

        return result.Succeeded ? "The event has been cancelled." : "The event was not cancelled: " + string.Join(" ", result.Errors.Select(error => error.Message));
    }
}
