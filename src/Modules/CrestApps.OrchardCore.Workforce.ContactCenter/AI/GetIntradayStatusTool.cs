using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Gives a planning group's day as the Intraday page shows it: forecast against actual work and agents needed against
/// scheduled and available per 15 minutes, the reforecast factor and the overtime and time-off offers.
/// </summary>
internal sealed class GetIntradayStatusTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "getIntradayStatus";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "planningGroupName": { "type": "string", "description": "The planning group's name or ID. Can be left out when there is only one." },
        "date": { "type": "string", "description": "Optional. The day, yyyy-MM-dd, in the planning group's team time zone. Today by default." },
        "onlyShort": { "type": "boolean", "description": "Optional. Only the intervals still to come where fewer agents are scheduled than needed." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "For a planning group and day, compares forecast with actual contacts and handle time, and agents needed with scheduled and available, per 15 minutes; gives the reforecast factor for the rest of the day and lists the open overtime and time-off offers.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageForecasts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (group, error) = await FindPlanningGroupAsync(services, arguments, cancellationToken);

        if (group is null)
        {
            return error;
        }

        if (Has(arguments, "date") && ReadDate(arguments, "date") is null)
        {
            return "The date must be a day written yyyy-MM-dd.";
        }

        var intraday = services.GetRequiredService<IntradayService>();
        var zone = await intraday.TimeZoneOfAsync(group, cancellationToken);
        var date = ReadDate(arguments, "date") ?? Today(services, zone);
        var rows = await intraday.DayAsync(group, date, cancellationToken);
        var factor = IntradayCalculator.ReforecastFactor(rows);
        var dayStart = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var offers = await intraday.ListAsync(group.ItemId, dayStart, CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone), cancellationToken);
        var workers = services.GetRequiredService<IStaffMemberManager>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var workerId in offers.SelectMany(offer => offer.Claims).Select(claim => claim.WorkerId).Distinct(StringComparer.Ordinal))
        {
            names[workerId] = (await workers.FindByIdAsync(workerId, cancellationToken))?.DisplayName ?? workerId;
        }

        var shown = ReadBool(arguments, "onlyShort") == true
            ? rows.Where(row => !row.Elapsed && row.Gap > 0).ToList()
            : rows.ToList();
        var elapsed = rows.Where(row => row.Elapsed).ToList();
        var now = services.GetRequiredService<IClock>().UtcNow;

        return ToJson(new
        {
            planningGroup = new { id = group.ItemId, name = group.Name },
            date = Day(date),
            timeZone = zone,
            reforecastFactor = Math.Round(factor, 3),
            reforecastChangePercent = Math.Round((factor - 1) * 100, 1),
            soFar = new
            {
                forecastContacts = Math.Round(elapsed.Sum(row => row.ForecastVolume), 1),
                actualContacts = Math.Round(elapsed.Sum(row => row.ActualVolume ?? 0), 1),
            },
            intervalsShort = rows.Count(row => !row.Elapsed && row.Gap > 0),
            intervals = shown.Select(row => new
            {
                time = TimeInZone(row.StartUtc, zone),
                elapsed = row.Elapsed,
                forecastContacts = Math.Round(row.ForecastVolume, 1),
                actualContacts = row.ActualVolume is null ? (double?)null : Math.Round(row.ActualVolume.Value, 1),
                forecastHandleSeconds = Math.Round(row.ForecastHandleSeconds),
                actualHandleSeconds = row.ActualHandleSeconds is null ? (double?)null : Math.Round(row.ActualHandleSeconds.Value),
                agentsNeeded = row.Required,
                agentsToSchedule = row.RequiredScheduled,
                scheduled = row.Scheduled,
                available = row.Available is null ? (double?)null : Math.Round(row.Available.Value, 1),
                gap = row.Gap,
                serviceLevelPercent = Percent(row.ServiceLevel),
            }),
            offers = offers.Select(offer => new
            {
                id = offer.ItemId,
                kind = offer.Kind.ToString(),
                start = InZone(offer.StartUtc, zone),
                end = InZone(offer.EndUtc, zone),
                places = offer.Places,
                taken = offer.Claims.Count,
                takenBy = offer.Claims.Select(claim => names.GetValueOrDefault(claim.WorkerId, claim.WorkerId)),
                status = offer.EndUtc <= now && offer.Status == IntradayOfferStatus.Open ? "Over" : offer.Status.ToString(),
                note = offer.Note,
            }),
        });
    }
}
