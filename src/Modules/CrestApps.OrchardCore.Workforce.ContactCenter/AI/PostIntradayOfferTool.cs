using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Offers voluntary overtime or voluntary time off to the team of a planning group, as the Intraday page's offer form
/// does. Workers take places in My Work, which changes their shifts and their pay, so it asks for approval.
/// </summary>
internal sealed class PostIntradayOfferTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "postIntradayOffer";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "planningGroupName": { "type": "string", "description": "The planning group's name or ID; the offer goes to its team. Can be left out when there is only one." },
        "kind": { "type": "string", "enum": ["VoluntaryOvertime", "VoluntaryTimeOff"], "description": "Voluntary overtime (extra time next to a shift) or voluntary time off (time cut from a shift)." },
        "date": { "type": "string", "description": "The day, yyyy-MM-dd, in the planning group's team time zone." },
        "start": { "type": "string", "description": "The start time, HH:mm." },
        "end": { "type": "string", "description": "The end time, HH:mm. An end at or before the start is on the next day." },
        "places": { "type": "integer", "minimum": 1, "description": "How many workers can take it." },
        "note": { "type": "string", "description": "Optional. A note workers see with the offer." }
      },
      "required": ["kind", "date", "start", "end", "places"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Offers voluntary overtime or voluntary time off to a planning group's team for a time range, for a number of places. Workers see it in My Work and take a place, first come first served, until none are left.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageForecasts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (group, error) = await FindPlanningGroupAsync(services, arguments, cancellationToken);

        if (group is null)
        {
            return error;
        }

        if (!TryParseEnum<IntradayOfferKind>(Read(arguments, "kind"), out var kind))
        {
            return "The kind must be VoluntaryOvertime or VoluntaryTimeOff.";
        }

        var date = ReadDate(arguments, "date");
        var start = ReadTime(arguments, "start");
        var end = ReadTime(arguments, "end");

        if (date is null || start is null || end is null)
        {
            return "Give the day (yyyy-MM-dd) and the start and end times (HH:mm) in the planning group's team time zone.";
        }

        var places = ReadInt(arguments, "places");

        if (places is null or < 1)
        {
            return "Offer at least one place (places).";
        }

        var intraday = services.GetRequiredService<IntradayService>();
        var zone = await intraday.TimeZoneOfAsync(group, cancellationToken);
        var endLocal = end.Value <= start.Value ? date.Value.AddDays(1).ToDateTime(end.Value) : date.Value.ToDateTime(end.Value);
        var offer = new IntradayOffer
        {
            PlanningGroupId = group.ItemId,
            Kind = kind,
            StartUtc = CalendarTime.ToUtc(date.Value.ToDateTime(start.Value), zone),
            EndUtc = CalendarTime.ToUtc(endLocal, zone),
            Places = places.Value,
            Note = Read(arguments, "note"),
            CreatedBy = user.Identity?.Name,
        };
        var errors = await intraday.OfferAsync(offer, cancellationToken);

        if (errors.Count > 0)
        {
            return "Nothing was offered: " + string.Join(" ", errors);
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return ToJson(new
        {
            offered = true,
            id = offer.ItemId,
            planningGroup = group.Name,
            kind = offer.Kind.ToString(),
            start = InZone(offer.StartUtc, zone),
            end = InZone(offer.EndUtc, zone),
            timeZone = zone,
            places = offer.Places,
            note = offer.Note,
            message = "The offer is in the team's My Work. Workers take it first come, first served.",
        });
    }
}
