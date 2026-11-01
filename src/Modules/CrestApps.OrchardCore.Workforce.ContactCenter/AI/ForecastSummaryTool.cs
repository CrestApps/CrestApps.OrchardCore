using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Summarizes the active forecast of planning groups for a day: the contacts expected, their handle time, and the most
/// agents needed and when.
/// </summary>
public sealed class ForecastSummaryTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceForecastSummary";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "planningGroupName": { "type": "string", "description": "Optional. One planning group; leave out for every planning group." },
            "date": { "type": "string", "description": "The day, as yyyy-MM-dd in each planning group's time zone. Leave out for today." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Summarizes the active contact center forecast of planning groups for a day: the contacts expected, the average handle time, when the work starts and ends, and the most agents to schedule and when, in each planning group's time zone.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkforcePermissions.ManageForecasts))
        {
            return "You aren't allowed to see forecasts.";
        }

        var groups = (await services.GetRequiredService<PlanningGroupManager>().GetAllAsync(cancellationToken)).OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var groupName = Read(arguments, "planningGroupName");

        if (groupName is not null)
        {
            groups = groups.Where(group => WorkforceNames.Normalize(group.Name) == WorkforceNames.Normalize(groupName)).ToList();

            if (groups.Count == 0)
            {
                return $"No planning group is called '{groupName}'.";
            }
        }

        var date = ReadDate(arguments, "date");
        var forecasts = services.GetRequiredService<ForecastService>();
        var timeZones = services.GetRequiredService<IWorkerTimeZoneService>();
        var clock = services.GetRequiredService<IClock>();
        var results = new List<object>();

        foreach (var group in groups)
        {
            var zone = await timeZones.GetTeamAsync(group.TeamId, cancellationToken);
            var day = date ?? DateOnly.FromDateTime(CalendarTime.ToLocal(clock.UtcNow, zone));
            var fromUtc = CalendarTime.ToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
            var toUtc = CalendarTime.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
            var intervals = await forecasts.ActiveIntervalsAsync(group.ItemId, fromUtc, toUtc, cancellationToken);
            var busy = intervals.Where(interval => interval.Volume > 0).ToList();

            if (busy.Count == 0)
            {
                results.Add(new { planningGroup = group.Name, date = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), forecast = "No active forecast has work on this day." });

                continue;
            }

            var volume = busy.Sum(interval => interval.Volume);
            var peak = busy.OrderByDescending(interval => interval.RequiredScheduled).ThenBy(interval => interval.StartUtc).First();

            results.Add(new
            {
                planningGroup = group.Name,
                date = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                contacts = Math.Round(volume),
                averageHandleSeconds = Math.Round(busy.Sum(interval => interval.Volume * interval.AverageHandleSeconds) / volume),
                firstWork = Local(CalendarTime.ToLocal(busy[0].StartUtc, zone)),
                lastWork = Local(CalendarTime.ToLocal(busy[^1].StartUtc.AddMinutes(15), zone)),
                mostAgentsToSchedule = peak.RequiredScheduled,
                mostAgentsAt = Local(CalendarTime.ToLocal(peak.StartUtc, zone)),
                agentHours = Math.Round(busy.Sum(interval => interval.RequiredScheduled) / 4d, 1),
            });
        }

        return JsonSerializer.Serialize(new { forecasts = results }, JsonOptions);
    }
}
