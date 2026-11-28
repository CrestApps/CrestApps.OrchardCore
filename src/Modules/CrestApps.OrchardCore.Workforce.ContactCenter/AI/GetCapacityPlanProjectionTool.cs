using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Gives a capacity plan's week-by-week projection, as the capacity plan's page shows it.
/// </summary>
internal sealed class GetCapacityPlanProjectionTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "getCapacityPlanProjection";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "capacityPlanId": { "type": "string", "description": "The capacity plan's ID. Give this or capacityPlanName." },
        "capacityPlanName": { "type": "string", "description": "The capacity plan's name, when the ID isn't known." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gives a capacity plan's week-by-week projection: the workload, the full-time agents needed, those available and those in training, the gap, and the first and worst weeks the plan runs short.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageForecasts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<CapacityPlanManager>();
        var id = Read(arguments, "capacityPlanId");
        var name = Read(arguments, "capacityPlanName");
        CapacityPlan plan;

        if (id is not null)
        {
            plan = await manager.FindByIdAsync(id, cancellationToken);

            if (plan is null)
            {
                return $"No capacity plan has the ID '{id}'.";
            }
        }
        else if (name is not null)
        {
            var plans = (await manager.GetAllAsync(cancellationToken)).ToList();
            var matches = plans.Where(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(name)).ToList();

            if (matches.Count != 1)
            {
                return matches.Count == 0
                    ? $"No capacity plan is called '{name}'. The capacity plans are: {string.Join(", ", plans.Select(item => item.Name))}."
                    : $"Several capacity plans are called '{name}': {string.Join(", ", matches.Select(item => item.ItemId))}. Give the capacityPlanId.";
            }

            plan = matches[0];
        }
        else
        {
            return "Give the capacity plan's ID (capacityPlanId) or name (capacityPlanName).";
        }

        var projection = await services.GetRequiredService<CapacityPlanService>().ProjectAsync(plan, cancellationToken);

        return ToJson(new
        {
            id = plan.ItemId,
            name = plan.Name,
            planningGroup = projection.Group?.Name ?? plan.PlanningGroupId,
            startDate = Day(plan.StartDate),
            weeks = plan.Weeks,
            today = Day(projection.Today),
            weeklyWorkloadHours = projection.WorkloadHours is null ? (double?)null : Math.Round(projection.WorkloadHours.Value, 1),
            workloadFromHistory = projection.WorkloadFromHistory,
            growthPercentPerYear = plan.GrowthPercentPerYear,
            hoursPerFte = plan.HoursPerFte,
            currentFte = plan.CurrentFte,
            attritionPercentPerMonth = plan.AttritionPercentPerMonth,
            hiringClasses = plan.HiringClasses.Select(hiring => new { startDate = Day(hiring.StartDate), size = hiring.Size, trainingWeeks = hiring.TrainingWeeks, rampWeeks = hiring.RampWeeks }),
            firstShortWeek = Day(projection.FirstShortWeek?.WeekStart),
            worstWeek = projection.WorstWeek is null ? null : new { weekStart = Day(projection.WorstWeek.WeekStart), gapFte = Math.Round(projection.WorstWeek.Gap, 1) },
            projection = projection.Weeks.Select(week => new
            {
                weekStart = Day(week.WeekStart),
                workloadHours = Math.Round(week.WorkloadHours, 1),
                requiredFte = Math.Round(week.RequiredFte, 1),
                availableFte = Math.Round(week.AvailableFte, 1),
                inTrainingFte = Math.Round(week.InTrainingFte, 1),
                gapFte = Math.Round(week.Gap, 1),
            }),
        });
    }
}
