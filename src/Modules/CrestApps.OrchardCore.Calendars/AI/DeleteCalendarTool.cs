using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Deletes a calendar the person manages, with every event on it, as Manage Calendars does.
/// </summary>
internal sealed class DeleteCalendarTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "deleteCalendar";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "calendarId": { "type": "string", "description": "The calendar's ID." }
      },
      "required": ["calendarId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Deletes a calendar and every event on it. There is no undo. Refused for a calendar a module manages (appointments, workforce teams, fleet assets).";

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

        if (level < CalendarAccessLevel.Manage)
        {
            return "The user can't delete that calendar: it needs the right to manage it.";
        }

        if (!string.IsNullOrEmpty(calendar.Source))
        {
            return $"The calendar is managed by {calendar.Source} and can't be deleted here.";
        }

        await services.GetRequiredService<ICalendarService>().DeleteAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(user) }, cancellationToken);

        return CalendarToolSupport.Json(new
        {
            deleted = true,
            calendarId = calendar.ItemId,
            calendar = calendar.Name,
            note = "The calendar and all of its events were deleted.",
        });
    }
}
