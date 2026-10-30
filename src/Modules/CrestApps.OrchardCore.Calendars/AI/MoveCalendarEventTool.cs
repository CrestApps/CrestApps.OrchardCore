using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Moves an event, or one occurrence of a repeating event, to another time.
/// </summary>
public sealed class MoveCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "moveCalendarEvent";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "eventId": { "type": "string" },
        "occurrenceStart": { "type": "string", "description": "For a repeating event, the occurrenceStart findCalendarEvents returned. Only that occurrence moves." },
        "start": { "type": "string", "description": "The new start, ISO 8601 with an offset." },
        "end": { "type": "string", "description": "The new end. Leave out to keep the length." }
      },
      "required": ["eventId", "start"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Moves an event the user can change to another time. Confirm the new time with the user first.";

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

        var errors = new List<string>();
        var input = new CalendarEventInput { Start = Read(arguments, "start"), End = Read(arguments, "end") };
        var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(user) };
        var result = DateTimeOffset.TryParse(Read(arguments, "occurrenceStart"), out var original)
            ? await eventManager.UpdateOccurrenceAsync(calendarEvent, original.UtcDateTime, CalendarEditScope.ThisOccurrence, target => CalendarEventInputApplier.Apply(input, target, errors), options)
            : await eventManager.UpdateAsync(calendarEvent, target => CalendarEventInputApplier.Apply(input, target, errors), options);

        if (errors.Count > 0)
        {
            return string.Join(" ", errors);
        }

        return result.Succeeded
            ? JsonSerializer.Serialize(CalendarWorkflowInput.ToDictionary(result.Value), JsonOptions)
            : "The event was not moved: " + string.Join(" ", result.Errors.Select(error => error.Message));
    }
}
