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
/// Lists the forecasts, newest first, as the Forecasts page does.
/// </summary>
internal sealed class ListForecastsTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "listForecasts";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "planningGroupName": { "type": "string", "description": "Optional. Only the forecasts of this planning group (name or ID)." },
        "page": { "type": "integer", "minimum": 1, "description": "Optional. The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Optional. Items per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the contact center forecasts of a planning group or of all, newest first, with their dates, model, status (draft, active or superseded), adjustments and accuracy against history.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageForecasts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        string groupId = null;

        if (Has(arguments, "planningGroupName"))
        {
            var (group, error) = await FindPlanningGroupAsync(services, arguments, cancellationToken);

            if (group is null)
            {
                return error;
            }

            groupId = group.ItemId;
        }

        var groups = (await services.GetRequiredService<PlanningGroupManager>().GetAllAsync(cancellationToken))
            .ToDictionary(group => group.ItemId, group => group.Name, StringComparer.Ordinal);
        var (page, pageSize) = ReadPaging(arguments);
        var (count, forecasts) = await services.GetRequiredService<ForecastService>().PageAsync(groupId, (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>(forecasts.Count);

        foreach (var forecast in forecasts)
        {
            items.Add(new
            {
                id = forecast.ItemId,
                planningGroup = groups.GetValueOrDefault(forecast.PlanningGroupId, forecast.PlanningGroupId),
                from = Day(forecast.FromDate),
                to = Day(forecast.ToDate),
                model = forecast.Model.ToString(),
                status = forecast.Status.ToString(),
                adjustments = forecast.Adjustments.Count,
                accuracy = forecast.Backtests.Select(test => new { model = test.Model.ToString(), mapePercent = Math.Round(test.Mape * 100, 1), wapePercent = Math.Round(test.Wape * 100, 1) }),
                createdBy = forecast.CreatedBy,
                createdAt = await LocalAsync(services, forecast.CreatedUtc),
                activatedAt = await LocalAsync(services, forecast.ActivatedUtc),
            });
        }

        return ToJson(Paged(items, count, page, pageSize));
    }
}
