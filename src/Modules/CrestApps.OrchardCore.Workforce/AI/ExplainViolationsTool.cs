using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Explains the labor rule problems of a team's schedule before it is published.
/// </summary>
public sealed class ExplainViolationsTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceExplainViolations";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "teamName": { "type": "string", "description": "The team whose schedule to check." },
            "date": { "type": "string", "description": "A day the schedule covers, as yyyy-MM-dd." }
          },
          "required": [ "teamName", "date" ],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Checks a team's draft schedule against its labor rules and lists each problem with the rule, the worker, the day and whether it warns, blocks or needs an override.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
        var teamName = Read(arguments, "teamName");
        var date = ReadDate(arguments, "date");
        var team = (await services.GetRequiredService<ITeamManager>().GetAllAsync(cancellationToken)).FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

        if (team is null || !date.HasValue)
        {
            return "Give a team you manage and a day of its schedule.";
        }

        if (!scope.Covers(team.ItemId))
        {
            return $"You don't manage {team.Name}.";
        }

        var planning = services.GetRequiredService<ISchedulePlanningService>();
        var (_, periods) = await planning.ListPeriodsAsync([team.ItemId], 0, 200, cancellationToken);
        var period = periods.FirstOrDefault(item => item.TeamId == team.ItemId && item.StartDate <= date.Value && item.EndDate >= date.Value);

        if (period is null)
        {
            return $"{team.Name} has no schedule on {date.Value:yyyy-MM-dd}.";
        }

        var violations = await planning.CheckPublishAsync(period.ItemId, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            schedule = period.Name,
            violations = violations.Select(violation => new
            {
                rule = violation.RuleName,
                worker = violation.WorkerName,
                date = violation.Date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                enforcement = violation.Enforcement.ToString(),
                message = violation.Message,
            }),
        }, JsonOptions);
    }
}
