using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Tells whether a business-hours calendar, a Business Hours schedule, is open now or at a given time, through the
/// business hours service routing uses, and when it next opens or closes.
/// </summary>
internal sealed class CheckBusinessHoursTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "checkBusinessHours";

    // How far ahead the next opening or closing is looked for.
    private static readonly TimeSpan _lookAhead = TimeSpan.FromDays(14);

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "calendar": {
          "type": "string",
          "description": "The calendar's ID, or its exact name."
        },
        "at": {
          "type": "string",
          "description": "The date and time to check, yyyy-MM-ddTHH:mm in the user's own time zone. Leave it out for now."
        }
      },
      "required": ["calendar"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Tells whether a Contact Center business-hours calendar is open now or at a given date and time, and when it next opens or closes. Use it to explain why calls are treated as after hours.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => BusinessHoursPermissions.ManageBusinessHoursSchedules;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var key = reader.GetString("calendar");

        if (key is null)
        {
            return "Give the calendar's ID or name in calendar.";
        }

        var calendar = await ContactCenterSetupLookups.FindAsync(services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>(), key, schedule => schedule.Name, cancellationToken);

        if (calendar is null)
        {
            return $"No business-hours calendar has the ID or name '{key}'. Use listRecords with the kind businessHoursCalendar to find it.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var at = reader.GetString("at");
        DateTime instantUtc;

        if (at is null)
        {
            instantUtc = services.GetRequiredService<IClock>().UtcNow;
        }
        else if (DateTime.TryParseExact(at, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            instantUtc = await localClock.ToUtcLenientAsync(local);
        }
        else
        {
            return "Give at as yyyy-MM-ddTHH:mm in the user's own time zone, or leave it out for now.";
        }

        var open = await services.GetRequiredService<IBusinessHoursService>().EvaluateAsync(calendar.ItemId, instantUtc, null, cancellationToken);

        if (open is null)
        {
            return Json(new
            {
                calendar = new { id = calendar.ItemId, name = calendar.Name },
                at = await ToLocalAsync(localClock, instantUtc),
                open = (bool?)null,
                explanation = "The calendar is disabled, so it does not gate routing: queues and entry points that use it are treated as open.",
            });
        }

        var windows = await services.GetRequiredService<IOpenHoursService>().GetOpenWindowsAsync(calendar.ItemId, instantUtc, instantUtc.Add(_lookAhead), cancellationToken: cancellationToken) ?? [];
        var current = windows.FirstOrDefault(window => window.Contains(instantUtc));
        var next = windows.FirstOrDefault(window => window.StartUtc > instantUtc);
        DateTime? closesUtc = open.Value && current.EndUtc < instantUtc.Add(_lookAhead) && !current.IsEmpty ? current.EndUtc : null;
        DateTime? opensUtc = !open.Value && !next.IsEmpty ? next.StartUtc : null;

        return Json(new
        {
            calendar = new { id = calendar.ItemId, name = calendar.Name },
            timeZoneId = string.IsNullOrWhiteSpace(calendar.Hours?.TimeZoneId) ? "UTC" : calendar.Hours.TimeZoneId,
            at = await ToLocalAsync(localClock, instantUtc),
            open = open.Value,
            closes = await ToLocalAsync(localClock, closesUtc),
            opens = await ToLocalAsync(localClock, opensUtc),
            explanation = open.Value
                ? closesUtc is null ? "Open, and it stays open for at least the next two weeks." : "Open."
                : opensUtc is null ? "Closed, with no opening in the next two weeks." : "Closed: work that arrives now is handled by the after-hours or closed action.",
        });
    }
}
