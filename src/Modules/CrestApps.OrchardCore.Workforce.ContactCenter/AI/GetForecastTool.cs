using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Gets a forecast one day at a time, as the forecast's page shows it: its adjustments, its accuracy against history and
/// the day's 15-minute intervals.
/// </summary>
internal sealed class GetForecastTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "getForecast";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "forecastId": { "type": "string", "description": "The forecast's ID, from listForecasts." },
        "day": { "type": "string", "description": "Optional. The day to show, yyyy-MM-dd, inside the forecast's dates. Its first day by default." },
        "onlyBusy": { "type": "boolean", "description": "Optional. Only the intervals with contacts or agents needed (the default); false gives every interval of the day." }
      },
      "required": ["forecastId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets a forecast: its planning group, dates, model and status, its adjustments, its accuracy against history and, for one day, each 15-minute interval's contacts, handle time, agents needed and to schedule, service level and occupancy, with the day's totals and busiest interval.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageForecasts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = Read(arguments, "forecastId");

        if (id is null)
        {
            return "Give the forecast's ID (forecastId), from listForecasts.";
        }

        var forecast = await services.GetRequiredService<ForecastService>().FindAsync(id, cancellationToken);

        if (forecast is null)
        {
            return $"No forecast has the ID '{id}'.";
        }

        if (Has(arguments, "day") && ReadDate(arguments, "day") is null)
        {
            return "The day must be written yyyy-MM-dd.";
        }

        var day = ReadDate(arguments, "day") ?? forecast.Intervals.Select(interval => interval.Date).DefaultIfEmpty(forecast.FromDate).Min();

        if (day < forecast.FromDate || day > forecast.ToDate)
        {
            return $"The forecast covers {Day(forecast.FromDate)} to {Day(forecast.ToDate)}; choose a day in between.";
        }

        var group = await services.GetRequiredService<PlanningGroupManager>().FindByIdAsync(forecast.PlanningGroupId, cancellationToken);
        var zone = string.IsNullOrEmpty(forecast.TimeZoneId) ? "UTC" : forecast.TimeZoneId;
        var all = forecast.Intervals.Where(interval => interval.Date == day).OrderBy(interval => interval.StartUtc).ToList();
        var busy = all.Where(interval => interval.Volume > 0 || interval.RequiredAgents > 0).ToList();
        var shown = ReadBool(arguments, "onlyBusy") == false ? all : busy;
        var volume = busy.Sum(interval => interval.Volume);
        var peak = busy.OrderByDescending(interval => interval.RequiredScheduled).ThenBy(interval => interval.StartUtc).FirstOrDefault();

        return ToJson(new
        {
            id = forecast.ItemId,
            planningGroup = group?.Name ?? forecast.PlanningGroupId,
            from = Day(forecast.FromDate),
            to = Day(forecast.ToDate),
            timeZone = zone,
            model = forecast.Model.ToString(),
            status = forecast.Status.ToString(),
            shrinkagePercent = forecast.ShrinkagePercent,
            createdBy = forecast.CreatedBy,
            createdAt = await LocalAsync(services, forecast.CreatedUtc),
            activatedAt = await LocalAsync(services, forecast.ActivatedUtc),
            adjustments = forecast.Adjustments.Select(adjustment => new
            {
                from = Day(adjustment.FromDate),
                to = Day(adjustment.ToDate),
                percent = adjustment.Percent,
                note = adjustment.Note,
                by = adjustment.By,
            }),
            accuracy = forecast.Backtests.Select(test => new { model = test.Model.ToString(), mapePercent = Math.Round(test.Mape * 100, 1), wapePercent = Math.Round(test.Wape * 100, 1) }),
            day = new
            {
                date = Day(day),
                contacts = Math.Round(volume, 1),
                averageHandleSeconds = volume > 0 ? Math.Round(busy.Sum(interval => interval.Volume * interval.AverageHandleSeconds) / volume) : 0,
                agentHoursToSchedule = Math.Round(busy.Sum(interval => interval.RequiredScheduled) / 4d, 1),
                firstWork = busy.Count > 0 ? TimeInZone(busy[0].StartUtc, zone) : null,
                lastWork = busy.Count > 0 ? TimeInZone(busy[^1].StartUtc.AddMinutes(15), zone) : null,
                busiest = peak is null ? null : new { time = TimeInZone(peak.StartUtc, zone), agentsToSchedule = peak.RequiredScheduled, contacts = Math.Round(peak.Volume, 1) },
                intervals = shown.Select(interval => new
                {
                    time = TimeInZone(interval.StartUtc, zone),
                    contacts = Math.Round(interval.Volume, 1),
                    averageHandleSeconds = Math.Round(interval.AverageHandleSeconds),
                    agentsNeeded = interval.RequiredAgents,
                    agentsToSchedule = interval.RequiredScheduled,
                    serviceLevelPercent = Math.Round(interval.ServiceLevel * 100, 1),
                    averageSpeedOfAnswerSeconds = Math.Round(interval.AverageSpeedOfAnswerSeconds),
                    occupancyPercent = Math.Round(interval.Occupancy * 100, 1),
                }),
            },
            days = forecast.Intervals.Select(interval => interval.Date).Distinct().Order().Select(date => Day(date)),
        });
    }
}
