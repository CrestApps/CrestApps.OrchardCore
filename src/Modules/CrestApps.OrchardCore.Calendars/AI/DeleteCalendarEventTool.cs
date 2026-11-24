using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Permanently deletes an event the person can change, as the event editor's Delete button does.
/// </summary>
internal sealed class DeleteCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "deleteCalendarEvent";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "eventId": { "type": "string", "description": "The event's ID. For a repeating event, the whole series is deleted." }
      },
      "required": ["eventId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Permanently deletes an event (the whole series for a repeating event). Attendees with an email address are told it was cancelled when Calendar Invitations is on. Prefer cancelCalendarEvent, which keeps the event and tells the attendees.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (calendarEvent, calendar) = await GetCalendarEventTool.LoadAsync(services, Read(arguments, "eventId"), cancellationToken);

        if (calendarEvent is null || !await services.GetRequiredService<CalendarEventEditor>().CanChangeAsync(user, calendarEvent, calendar))
        {
            return "The user can't change that event.";
        }

        await services.GetRequiredService<ICalendarEventManager>().DeleteAsync(calendarEvent, new CalendarChangeOptions { Actor = CalendarUser.GetId(user) }, cancellationToken);

        return CalendarToolSupport.Json(new
        {
            deleted = true,
            eventId = calendarEvent.ItemId,
            title = calendarEvent.Title,
            calendar = calendar.Name,
            wholeSeries = calendarEvent.Recurrence is not null,
        });
    }
}
