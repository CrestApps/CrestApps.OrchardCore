using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Rescales the rest of a day's active forecast for a planning group from how the day has gone so far, as the Intraday
/// page's Reforecast button does.
/// </summary>
internal sealed class ReforecastIntradayTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "reforecastIntraday";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "planningGroupName": { "type": "string", "description": "The planning group's name or ID. Can be left out when there is only one." },
        "date": { "type": "string", "description": "Optional. The day, yyyy-MM-dd, in the planning group's team time zone. Today by default." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Rescales the rest of today's forecast for a planning group from how the day has gone so far, staffs it again (the team's coverage follows), and returns the factor used. Nothing changes when the day is close to the forecast.";

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
        var date = ReadDate(arguments, "date") ?? Today(services, await intraday.TimeZoneOfAsync(group, cancellationToken));
        var (factor, intervals) = await intraday.ReforecastAsync(group, date, user.Identity?.Name, cancellationToken);

        if (intervals == 0)
        {
            return ToJson(new
            {
                changed = false,
                planningGroup = group.Name,
                date = Day(date),
                factor = Math.Round(factor, 3),
                message = "The work so far is close to the forecast, or the rest of the day has none; nothing was changed.",
            });
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return ToJson(new
        {
            changed = true,
            planningGroup = group.Name,
            date = Day(date),
            factor = Math.Round(factor, 3),
            changePercent = Math.Round((factor - 1) * 100, 1),
            intervalsScaled = intervals,
            message = "The rest of the day was scaled and staffed again; the team's coverage follows.",
        });
    }
}
