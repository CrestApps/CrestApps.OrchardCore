using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Adds an event to one of the person's calendars, their personal calendar when none is named.
/// </summary>
public sealed class CreateCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "createCalendarEvent";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "title": { "type": "string" },
        "start": { "type": "string", "description": "ISO 8601 time with an offset, or a date (yyyy-MM-dd) for an all-day event." },
        "end": { "type": "string", "description": "ISO 8601 time with an offset, or the last day for an all-day event. Optional." },
        "calendarId": { "type": "string", "description": "The calendar to add to. Leave out for the user's personal calendar." },
        "location": { "type": "string" },
        "description": { "type": "string" },
        "attendees": { "type": "array", "items": { "type": "string" }, "description": "Emails, or user:{userId} for users of the site." }
      },
      "required": ["title", "start"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Adds an event to a calendar the user can add to. Read the time back to the user and get a clear yes before calling it. Fails when a room or calendar is already full at that time.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var calendarService = services.GetRequiredService<ICalendarService>();
        var eventManager = services.GetRequiredService<ICalendarEventManager>();
        var editor = services.GetRequiredService<CalendarEventEditor>();
        var calendarId = Read(arguments, "calendarId");
        var calendar = calendarId is null
            ? await calendarService.GetOrCreatePersonalCalendarAsync(CalendarUser.GetId(user), user.Identity?.Name, cancellationToken)
            : await calendarService.FindByIdAsync(calendarId, cancellationToken);

        if (calendar is null || !await editor.CanAddAsync(user, calendar))
        {
            return "The user can't add events to that calendar.";
        }

        var calendarEvent = await eventManager.NewAsync(calendar, cancellationToken: cancellationToken);
        var errors = new List<string>();

        CalendarEventInputApplier.Apply(new CalendarEventInput
        {
            Title = Read(arguments, "title"),
            Start = Read(arguments, "start"),
            End = Read(arguments, "end"),
            Location = Read(arguments, "location"),
            Description = Read(arguments, "description"),
            Attendees = ReadList(arguments, "attendees"),
        }, calendarEvent, errors);

        if (errors.Count > 0)
        {
            return string.Join(" ", errors);
        }

        var result = await eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = CalendarUser.GetId(user) }, cancellationToken);

        return result.Succeeded
            ? JsonSerializer.Serialize(CalendarWorkflowInput.ToDictionary(result.Value), JsonOptions)
            : "The event was not added: " + string.Join(" ", result.Errors.Select(error => error.Message));
    }
}
