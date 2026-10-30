using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Finds times when the person, and optionally other people and calendars, are free.
/// </summary>
public sealed class FindFreeTimeTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "findFreeTime";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "from": { "type": "string", "description": "Start of the range, ISO 8601 with an offset." },
        "to": { "type": "string", "description": "End of the range, ISO 8601 with an offset. At most 31 days after from." },
        "durationMinutes": { "type": "integer", "description": "How long the free time must be." },
        "userIds": { "type": "array", "items": { "type": "string" }, "description": "Other users who must also be free. The user is always included." },
        "calendarIds": { "type": "array", "items": { "type": "string" }, "description": "Calendars, such as a room, that must also be free." }
      },
      "required": ["from", "to", "durationMinutes"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Finds stretches of time when the user, and any other users and calendars named, are all free for at least the given number of minutes.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(Read(arguments, "from"), out var from) || !DateTimeOffset.TryParse(Read(arguments, "to"), out var to) || to <= from ||
            !int.TryParse(Read(arguments, "durationMinutes"), out var minutes) || minutes < 5)
        {
            return "Give from and to as ISO 8601 times and durationMinutes of at least 5.";
        }

        var accessService = services.GetRequiredService<ICalendarAccessService>();
        var calendarService = services.GetRequiredService<ICalendarService>();
        var calendarIds = new List<string>();

        foreach (var calendar in await calendarService.GetAsync(ReadList(arguments, "calendarIds"), cancellationToken))
        {
            if (await accessService.GetAccessLevelAsync(user, calendar, cancellationToken) >= CalendarAccessLevel.FreeBusy)
            {
                calendarIds.Add(calendar.ItemId);
            }
        }

        var userIds = ReadList(arguments, "userIds").Take(20).ToList();
        var self = CalendarUser.GetId(user);

        if (!string.IsNullOrEmpty(self) && !userIds.Contains(self))
        {
            userIds.Add(self);
        }

        var windows = await services.GetRequiredService<ICalendarAvailabilityService>().FindFreeWindowsAsync(new CalendarBusyQuery
        {
            CalendarIds = calendarIds,
            UserIds = userIds,
            FromUtc = from.UtcDateTime,
            ToUtc = to > from.AddDays(31) ? from.UtcDateTime.AddDays(31) : to.UtcDateTime,
        }, TimeSpan.FromMinutes(minutes), maxResults: 15, cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(windows.Select(window => new
        {
            startUtc = CalendarWorkflowInput.Format(window.StartUtc),
            endUtc = CalendarWorkflowInput.Format(window.EndUtc),
        }), JsonOptions);
    }
}
