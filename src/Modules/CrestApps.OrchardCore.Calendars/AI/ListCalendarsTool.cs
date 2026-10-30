using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Lists the calendars the person can see.
/// </summary>
public sealed class ListCalendarsTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "listCalendars";

    private static readonly JsonElement _schema = Schema("""{ "type": "object", "properties": {}, "additionalProperties": false }""");

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the calendars the user can see, with each calendar's id, type, time zone and whether the user can add events to it.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var calendars = await services.GetRequiredService<ICalendarAccessService>().ListAccessibleAsync(user, CalendarAccessLevel.FreeBusy);

        return JsonSerializer.Serialize(calendars.Select(access => new
        {
            id = access.Calendar.ItemId,
            name = access.Calendar.Name,
            type = access.Calendar.Type,
            timeZone = access.Calendar.TimeZoneId,
            canAddEvents = access.Calendar.Enabled && access.Level >= CalendarAccessLevel.Write,
            onlyFreeBusy = access.Level == CalendarAccessLevel.FreeBusy,
        }), JsonOptions);
    }
}
