using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Makes a new private subscription address for a calendar, or removes it, as the calendar's address page does.
/// </summary>
internal sealed class ResetCalendarFeedAddressTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "resetCalendarFeedAddress";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "calendarId": { "type": "string", "description": "The calendar's ID." },
        "action": { "type": "string", "enum": ["new", "remove"], "description": "new makes a new address (the old one stops working); remove takes the address away." }
      },
      "required": ["calendarId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Makes a new private subscription address for a calendar, or removes it. The old address stops working at once, so everyone subscribed must be given the new one. The new address is not returned: the calendar's manager copies it from the address page.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var action = Read(arguments, "action")?.ToLowerInvariant();

        if (action is not ("new" or "remove"))
        {
            return "action must be new or remove.";
        }

        var (calendar, level) = await CalendarToolSupport.FindCalendarAsync(services, user, Read(arguments, "calendarId"), cancellationToken);

        if (calendar is null)
        {
            return CalendarToolSupport.CalendarNotFound;
        }

        if (level < CalendarAccessLevel.Manage)
        {
            return "Only someone who manages the calendar can change its subscription address.";
        }

        var previous = calendar.FeedToken;
        var remove = action == "remove";

        CalendarFeedAddress.Reset(calendar, remove);

        var result = await services.GetRequiredService<ICalendarService>().UpdateAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(user) }, cancellationToken);

        if (!result.Succeeded)
        {
            calendar.FeedToken = previous;

            return "The subscription address was not changed: " + CalendarToolSupport.Describe(result.Errors);
        }

        return CalendarToolSupport.Json(new
        {
            calendarId = calendar.ItemId,
            calendar = calendar.Name,
            hasFeedAddress = !remove,
            addressPage = CalendarToolSupport.GetAdminPath(services, "CalendarFeeds", "Feed", new { id = calendar.ItemId }),
            note = remove
                ? "The subscription address has been removed. Anyone subscribed stops receiving the calendar."
                : string.IsNullOrEmpty(previous)
                    ? "A subscription address has been made. Copy it from the address page."
                    : "A new subscription address has been made. The old one no longer works; copy the new one from the address page.",
        });
    }
}
