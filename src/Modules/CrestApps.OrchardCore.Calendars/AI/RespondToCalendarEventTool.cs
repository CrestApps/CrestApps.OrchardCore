using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Records the person's own answer to an invitation, as the event editor's answer buttons do.
/// </summary>
internal sealed class RespondToCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "respondToCalendarEvent";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "eventId": { "type": "string", "description": "The event's ID." },
        "response": { "type": "string", "enum": ["Accepted", "Tentative", "Declined"], "description": "The user's answer." }
      },
      "required": ["eventId", "response"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Records the user's own answer to an invitation: accepted, tentative or declined. It cannot answer for someone else.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CalendarAttendeeResponse>(Read(arguments, "response"), ignoreCase: true, out var response) || response == CalendarAttendeeResponse.NeedsAction)
        {
            return "Use Accepted, Tentative or Declined.";
        }

        var userId = CalendarUser.GetId(user);
        var (calendarEvent, calendar) = await GetCalendarEventTool.LoadAsync(services, Read(arguments, "eventId"), cancellationToken);

        if (calendarEvent is null || string.IsNullOrEmpty(userId))
        {
            return "There is no event with that ID.";
        }

        var result = await services.GetRequiredService<ICalendarEventManager>().RespondAsync(calendarEvent, "user:" + userId, response, new CalendarChangeOptions { Actor = userId }, cancellationToken);

        if (!result.Succeeded)
        {
            return "Your answer could not be saved: " + CalendarToolSupport.Describe(result.Errors);
        }

        return CalendarToolSupport.Json(new
        {
            eventId = calendarEvent.ItemId,
            title = calendarEvent.Title,
            calendar = calendar.Name,
            response = response.ToString(),
        });
    }
}
